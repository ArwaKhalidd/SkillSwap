using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Conversations;

namespace SkillSwap.Application.Abstractions;

/// <summary>
/// Provides conversation and messaging operations.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Returns all conversations in which the caller is a participant.
    /// </summary>
    Task<Result<IReadOnlyList<ConversationDto>>> GetConversationsAsync(
        Guid callerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns paginated messages for a conversation the caller belongs to.
    /// </summary>
    Task<Result<IReadOnlyList<MessageDto>>> GetMessagesAsync(
        Guid callerId,
        long conversationId,
        int pageSize,
        long? beforeMessageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a new message in the specified conversation.
    /// </summary>
    Task<Result<MessageDto>> SendMessageAsync(
        Guid callerId,
        long conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all messages up to the latest as read for the caller.
    /// </summary>
    Task<Result> MarkReadAsync(
        Guid callerId,
        long conversationId,
        CancellationToken cancellationToken = default);
}
