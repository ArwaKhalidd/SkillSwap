using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Conversations;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Application.Services;

/// <summary>
/// Implements conversation listing, message retrieval, message sending, and read-receipt management.
/// </summary>
public class ConversationService : IConversationService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ConversationService(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<ConversationDto>>> GetConversationsAsync(
        Guid callerId,
        CancellationToken cancellationToken = default)
    {
        // Fetch participant rows for this user
        var participantRows = await _context.ConversationParticipants
            .AsNoTracking()
            .Where(cp => cp.UserId == callerId)
            .ToListAsync(cancellationToken);

        if (participantRows.Count == 0)
            return Result.Success<IReadOnlyList<ConversationDto>>(Array.Empty<ConversationDto>());

        var conversationIds = participantRows.Select(cp => cp.ConversationId).ToList();

        // Load conversations with participants and latest message in a single query
        var conversations = await _context.Conversations
            .AsNoTracking()
            .Where(c => conversationIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.SwapRequestId,
                c.CreatedAtUtc,
                ParticipantIds = c.Participants.Select(p => p.UserId).ToList(),
                LastMessage = c.Messages
                    .Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.Id)
                    .Select(m => new { m.Id, m.ConversationId, m.SenderId, m.Body, m.SentAtUtc })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Build a lookup for unread counts: messages after LastReadMessageId per conversation
        var participantByConv = participantRows.ToDictionary(cp => cp.ConversationId);

        // Fetch unread message counts for all conversations in one query
        var latestMessageIds = await _context.Messages
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId) && !m.IsDeleted)
            .GroupBy(m => m.ConversationId)
            .Select(g => new
            {
                ConversationId = g.Key,
                UnreadCount = g.Count(m =>
                    !participantByConv.ContainsKey(g.Key) ||
                    participantByConv[g.Key].LastReadMessageId == null ||
                    m.Id > participantByConv[g.Key].LastReadMessageId)
            })
            .ToListAsync(cancellationToken);

        var unreadByConv = latestMessageIds.ToDictionary(x => x.ConversationId, x => x.UnreadCount);

        var dtos = conversations
            .OrderByDescending(c => c.LastMessage?.SentAtUtc ?? c.CreatedAtUtc)
            .Select(c => new ConversationDto(
                c.Id,
                c.SwapRequestId,
                c.ParticipantIds,
                unreadByConv.GetValueOrDefault(c.Id, 0),
                c.CreatedAtUtc,
                c.LastMessage is null ? null : new MessageDto(
                    c.LastMessage.Id,
                    c.LastMessage.ConversationId,
                    c.LastMessage.SenderId,
                    c.LastMessage.Body,
                    c.LastMessage.SentAtUtc)))
            .ToList();

        return Result.Success<IReadOnlyList<ConversationDto>>(dtos);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<MessageDto>>> GetMessagesAsync(
        Guid callerId,
        long conversationId,
        int pageSize,
        long? beforeMessageId,
        CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 100)
            return Result.Failure<IReadOnlyList<MessageDto>>("Page size must be between 1 and 100.");

        // Authorization: caller must be a participant
        var isParticipant = await _context.ConversationParticipants
            .AsNoTracking()
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == callerId, cancellationToken);

        if (!isParticipant)
            return Result.Failure<IReadOnlyList<MessageDto>>("Conversation not found or access denied.");

        var query = _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted);

        if (beforeMessageId.HasValue)
            query = query.Where(m => m.Id < beforeMessageId.Value);

        var messages = await query
            .OrderByDescending(m => m.Id)
            .Take(pageSize)
            .Select(m => new MessageDto(m.Id, m.ConversationId, m.SenderId, m.Body, m.SentAtUtc))
            .ToListAsync(cancellationToken);

        // Return in ascending chronological order (oldest first)
        messages.Reverse();

        return Result.Success<IReadOnlyList<MessageDto>>(messages);
    }

    /// <inheritdoc/>
    public async Task<Result<MessageDto>> SendMessageAsync(
        Guid callerId,
        long conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        // Authorization: caller must be a participant
        var isParticipant = await _context.ConversationParticipants
            .AsNoTracking()
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == callerId, cancellationToken);

        if (!isParticipant)
            return Result.Failure<MessageDto>("Conversation not found or access denied.");

        var body = request.Body.Trim();
        if (string.IsNullOrEmpty(body))
            return Result.Failure<MessageDto>("Message body cannot be empty.");

        if (body.Length > 4000)
            return Result.Failure<MessageDto>("Message body cannot exceed 4000 characters.");

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = callerId,
            Body = body,
            SentAtUtc = _dateTimeProvider.UtcNow,
            IsDeleted = false
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new MessageDto(
            message.Id,
            message.ConversationId,
            message.SenderId,
            message.Body,
            message.SentAtUtc);

        return Result.Success(dto);
    }

    /// <inheritdoc/>
    public async Task<Result> MarkReadAsync(
        Guid callerId,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(
                cp => cp.ConversationId == conversationId && cp.UserId == callerId,
                cancellationToken);

        if (participant is null)
            return Result.Failure("Conversation not found or access denied.");

        // Get the latest non-deleted message id
        var latestMessageId = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderByDescending(m => m.Id)
            .Select(m => (long?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestMessageId.HasValue && participant.LastReadMessageId != latestMessageId)
        {
            participant.LastReadMessageId = latestMessageId;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
