using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Conversations;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

// ─────────────────────────────────────────────────────────────────────────────
// ConversationsControllerTests
// Tests controller routing, HTTP status codes, [Authorize] decoration, and
// basic delegation to the service layer (via a fake).
// ─────────────────────────────────────────────────────────────────────────────

public class ConversationsControllerTests
{
    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; init; }
        public string? Email => null;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    private sealed class FakeConversationService : IConversationService
    {
        public Func<Guid, CancellationToken, Task<Result<IReadOnlyList<ConversationDto>>>>? OnGetConversations { get; init; }
        public Func<Guid, long, int, long?, CancellationToken, Task<Result<IReadOnlyList<MessageDto>>>>? OnGetMessages { get; init; }
        public Func<Guid, long, SendMessageRequest, CancellationToken, Task<Result<MessageDto>>>? OnSendMessage { get; init; }
        public Func<Guid, long, CancellationToken, Task<Result>>? OnMarkRead { get; init; }

        public Task<Result<IReadOnlyList<ConversationDto>>> GetConversationsAsync(Guid callerId, CancellationToken ct)
            => OnGetConversations!(callerId, ct);

        public Task<Result<IReadOnlyList<MessageDto>>> GetMessagesAsync(Guid callerId, long conversationId, int pageSize, long? beforeMessageId, CancellationToken ct)
            => OnGetMessages!(callerId, conversationId, pageSize, beforeMessageId, ct);

        public Task<Result<MessageDto>> SendMessageAsync(Guid callerId, long conversationId, SendMessageRequest request, CancellationToken ct)
            => OnSendMessage!(callerId, conversationId, request, ct);

        public Task<Result> MarkReadAsync(Guid callerId, long conversationId, CancellationToken ct)
            => OnMarkRead!(callerId, conversationId, ct);
    }

    private static ConversationsController BuildController(
        IConversationService svc,
        Guid? userId = null)
    {
        var currentUser = new FakeCurrentUserService { UserId = userId ?? Guid.NewGuid() };
        return new ConversationsController(svc, currentUser);
    }

    // ── Authorization ─────────────────────────────────────────────────────────

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var attributes = typeof(ConversationsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);

        Assert.NotEmpty(attributes);
    }

    // ── GET /api/conversations ────────────────────────────────────────────────

    [Fact]
    public async Task GetConversations_ReturnsOkWithList()
    {
        var callerId = Guid.NewGuid();
        var expected = new List<ConversationDto>
        {
            new(1, 10, [callerId, Guid.NewGuid()], 3, DateTime.UtcNow.AddDays(-1), null)
        };

        var fake = new FakeConversationService
        {
            OnGetConversations = (id, ct) =>
                Task.FromResult(Result.Success<IReadOnlyList<ConversationDto>>(expected))
        };

        var controller = BuildController(fake, callerId);
        var result = await controller.GetConversations(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ConversationDto>>(ok.Value);
        Assert.Single(list);
        Assert.Equal(1, list[0].Id);
    }

    [Fact]
    public async Task GetConversations_EmptyList_ReturnsOkWithEmptyCollection()
    {
        var fake = new FakeConversationService
        {
            OnGetConversations = (_, ct) =>
                Task.FromResult(Result.Success<IReadOnlyList<ConversationDto>>(Array.Empty<ConversationDto>()))
        };

        var controller = BuildController(fake);
        var result = await controller.GetConversations(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ConversationDto>>(ok.Value);
        Assert.Empty(list);
    }

    // ── GET /api/conversations/{id}/messages ──────────────────────────────────

    [Fact]
    public async Task GetMessages_ValidConversation_ReturnsOkWithMessages()
    {
        var callerId = Guid.NewGuid();
        var convId = 42L;
        var msgs = new List<MessageDto>
        {
            new(1, convId, callerId, "Hello", DateTime.UtcNow)
        };

        var fake = new FakeConversationService
        {
            OnGetMessages = (id, cid, ps, before, ct) =>
                Task.FromResult(Result.Success<IReadOnlyList<MessageDto>>(msgs))
        };

        var controller = BuildController(fake, callerId);
        var result = await controller.GetMessages(convId, pageSize: 20, beforeMessageId: null, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<MessageDto>>(ok.Value);
        Assert.Single(list);
        Assert.Equal("Hello", list[0].Body);
    }

    [Fact]
    public async Task GetMessages_AccessDenied_Returns403()
    {
        var fake = new FakeConversationService
        {
            OnGetMessages = (_, _, _, _, _) =>
                Task.FromResult(Result.Failure<IReadOnlyList<MessageDto>>("Conversation not found or access denied."))
        };

        var controller = BuildController(fake);
        var result = await controller.GetMessages(99, pageSize: 20, beforeMessageId: null, default);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task GetMessages_InvalidPageSize_ReturnsBadRequest()
    {
        var fake = new FakeConversationService
        {
            OnGetMessages = (_, _, _, _, _) =>
                Task.FromResult(Result.Failure<IReadOnlyList<MessageDto>>("Page size must be between 1 and 100."))
        };

        var controller = BuildController(fake);
        var result = await controller.GetMessages(1, pageSize: 0, beforeMessageId: null, default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── POST /api/conversations/{id}/messages ─────────────────────────────────

    [Fact]
    public async Task SendMessage_ValidRequest_Returns201WithDto()
    {
        var callerId = Guid.NewGuid();
        var convId = 5L;
        var sentAt = DateTime.UtcNow;
        var msgDto = new MessageDto(100, convId, callerId, "Hi there!", sentAt);

        var fake = new FakeConversationService
        {
            OnSendMessage = (_, _, req, _) =>
                Task.FromResult(Result.Success(msgDto))
        };

        var controller = BuildController(fake, callerId);
        var result = await controller.SendMessage(convId, new SendMessageRequest("Hi there!"), default);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var returned = Assert.IsType<MessageDto>(created.Value);
        Assert.Equal("Hi there!", returned.Body);
        Assert.Equal(callerId, returned.SenderId);
    }

    [Fact]
    public async Task SendMessage_AccessDenied_Returns403()
    {
        var fake = new FakeConversationService
        {
            OnSendMessage = (_, _, _, _) =>
                Task.FromResult(Result.Failure<MessageDto>("Conversation not found or access denied."))
        };

        var controller = BuildController(fake);
        var result = await controller.SendMessage(99, new SendMessageRequest("Hello"), default);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task SendMessage_ServiceFailure_ReturnsBadRequest()
    {
        var fake = new FakeConversationService
        {
            OnSendMessage = (_, _, _, _) =>
                Task.FromResult(Result.Failure<MessageDto>("Message body cannot be empty."))
        };

        var controller = BuildController(fake);
        var result = await controller.SendMessage(1, new SendMessageRequest(""), default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("empty", bad.Value?.ToString());
    }

    // ── POST /api/conversations/{id}/read ────────────────────────────────────

    [Fact]
    public async Task MarkRead_ValidRequest_ReturnsOk()
    {
        var fake = new FakeConversationService
        {
            OnMarkRead = (_, _, _) => Task.FromResult(Result.Success())
        };

        var controller = BuildController(fake);
        var result = await controller.MarkRead(1, default);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task MarkRead_AccessDenied_Returns403()
    {
        var fake = new FakeConversationService
        {
            OnMarkRead = (_, _, _) =>
                Task.FromResult(Result.Failure("Conversation not found or access denied."))
        };

        var controller = BuildController(fake);
        var result = await controller.MarkRead(99, default);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ConversationServiceTests
// Validates service logic against in-memory EF Core database.
// ─────────────────────────────────────────────────────────────────────────────

public class ConversationServiceTests
{
    private static TestDateTimeProvider Dtp() => new();

    /// <summary>
    /// Seeds a conversation with two participants and optional messages.
    /// Returns (context, conversationId, userId1, userId2).
    /// </summary>
    private static async Task<(
        SkillSwap.Infrastructure.Persistence.ApplicationDbContext Ctx,
        long ConvId,
        Guid User1,
        Guid User2)> SeedConversationAsync(int messageCount = 0)
    {
        var ctx = TestDbContextFactory.CreateInMemoryContext();
        var dtp = Dtp();

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var category = new Category { Name = "Cat", IsActive = true, CreatedAtUtc = dtp.UtcNow };
        ctx.Categories.Add(category);
        await ctx.SaveChangesAsync();

        var skill = new Skill { CategoryId = category.Id, Name = "Skill", IsActive = true, CreatedAtUtc = dtp.UtcNow };
        ctx.Skills.Add(skill);
        await ctx.SaveChangesAsync();

        var swap = new SwapRequest
        {
            RequesterId = user1,
            ReceiverId = user2,
            SkillId = skill.Id,
            Status = SkillSwap.Domain.Enums.SwapRequestStatus.Accepted,
            CreatedAtUtc = dtp.UtcNow.AddDays(-2),
            RespondedAtUtc = dtp.UtcNow.AddDays(-1)
        };
        ctx.SwapRequests.Add(swap);
        await ctx.SaveChangesAsync();

        var conv = new Conversation { SwapRequestId = swap.Id, CreatedAtUtc = dtp.UtcNow.AddDays(-1) };
        ctx.Conversations.Add(conv);
        await ctx.SaveChangesAsync();

        ctx.ConversationParticipants.AddRange(
            new ConversationParticipant { ConversationId = conv.Id, UserId = user1, JoinedAtUtc = dtp.UtcNow },
            new ConversationParticipant { ConversationId = conv.Id, UserId = user2, JoinedAtUtc = dtp.UtcNow }
        );

        for (int i = 0; i < messageCount; i++)
        {
            ctx.Messages.Add(new Message
            {
                ConversationId = conv.Id,
                SenderId = i % 2 == 0 ? user1 : user2,
                Body = $"Message {i + 1}",
                SentAtUtc = dtp.UtcNow.AddMinutes(i),
                IsDeleted = false
            });
        }

        await ctx.SaveChangesAsync();
        return (ctx, conv.Id, user1, user2);
    }

    // ── GetConversationsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetConversations_ReturnsConversationsForParticipant()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync(messageCount: 2);
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.GetConversationsAsync(user1);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(convId, result.Value[0].Id);
        Assert.Contains(user1, result.Value[0].ParticipantIds);
    }

    [Fact]
    public async Task GetConversations_UnknownUser_ReturnsEmptyList()
    {
        var (ctx, _, _, _) = await SeedConversationAsync();
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.GetConversationsAsync(Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetConversations_LastMessagePopulated_WhenMessagesExist()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync(messageCount: 3);
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.GetConversationsAsync(user1);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value[0].LastMessage);
        Assert.Equal("Message 3", result.Value[0].LastMessage!.Body);
    }

    // ── GetMessagesAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetMessages_Participant_ReturnsMessages()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync(messageCount: 5);
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.GetMessagesAsync(user1, convId, 10, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.Count);
    }

    [Fact]
    public async Task GetMessages_NonParticipant_ReturnsAccessDenied()
    {
        var (ctx, convId, _, _) = await SeedConversationAsync(messageCount: 1);
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.GetMessagesAsync(Guid.NewGuid(), convId, 10, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("access denied", result.Error);
    }

    [Fact]
    public async Task GetMessages_InvalidPageSize_ReturnsFailure()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync();
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.GetMessagesAsync(user1, convId, 0, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Page size", result.Error);
    }

    [Fact]
    public async Task GetMessages_CursorPagination_ReturnsMessagesBeforeId()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync(messageCount: 10);
        var svc = new ConversationService(ctx, Dtp());

        // Get all to find a message id to pivot on
        var allResult = await svc.GetMessagesAsync(user1, convId, 100, null);
        Assert.True(allResult.IsSuccess);

        // Use the 6th message id as the cursor (0-indexed 5th)
        var pivotId = allResult.Value[5].Id;
        var beforeResult = await svc.GetMessagesAsync(user1, convId, 100, pivotId);

        Assert.True(beforeResult.IsSuccess);
        Assert.True(beforeResult.Value.All(m => m.Id < pivotId));
    }

    [Fact]
    public async Task GetMessages_ReturnedInAscendingOrder()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync(messageCount: 5);
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.GetMessagesAsync(user1, convId, 10, null);

        Assert.True(result.IsSuccess);
        var ids = result.Value.Select(m => m.Id).ToList();
        Assert.Equal(ids.OrderBy(x => x).ToList(), ids);
    }

    // ── SendMessageAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_Participant_PersistsAndReturnsDto()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync();
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.SendMessageAsync(user1, convId, new SendMessageRequest("Hello world"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello world", result.Value.Body);
        Assert.Equal(user1, result.Value.SenderId);
        Assert.Equal(convId, result.Value.ConversationId);

        // Verify persisted in DB
        var msg = ctx.Messages.SingleOrDefault(m => m.Id == result.Value.Id);
        Assert.NotNull(msg);
        Assert.Equal("Hello world", msg!.Body);
    }

    [Fact]
    public async Task SendMessage_NonParticipant_ReturnsFailure()
    {
        var (ctx, convId, _, _) = await SeedConversationAsync();
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.SendMessageAsync(Guid.NewGuid(), convId, new SendMessageRequest("Intruder!"));

        Assert.False(result.IsSuccess);
        Assert.Contains("access denied", result.Error);
    }

    [Fact]
    public async Task SendMessage_WhitespaceBody_ReturnsFailure()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync();
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.SendMessageAsync(user1, convId, new SendMessageRequest("   "));

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.Error);
    }

    [Fact]
    public async Task SendMessage_BodyTooLong_ReturnsFailure()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync();
        var svc = new ConversationService(ctx, Dtp());

        var longBody = new string('x', 4001);
        var result = await svc.SendMessageAsync(user1, convId, new SendMessageRequest(longBody));

        Assert.False(result.IsSuccess);
        Assert.Contains("4000", result.Error);
    }

    // ── MarkReadAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkRead_Participant_UpdatesLastReadMessageId()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync(messageCount: 3);
        var svc = new ConversationService(ctx, Dtp());

        var latestMsg = ctx.Messages
            .Where(m => m.ConversationId == convId)
            .OrderByDescending(m => m.Id)
            .First();

        var result = await svc.MarkReadAsync(user1, convId);

        Assert.True(result.IsSuccess);

        var participant = ctx.ConversationParticipants
            .First(cp => cp.ConversationId == convId && cp.UserId == user1);
        Assert.Equal(latestMsg.Id, participant.LastReadMessageId);
    }

    [Fact]
    public async Task MarkRead_NonParticipant_ReturnsFailure()
    {
        var (ctx, convId, _, _) = await SeedConversationAsync(messageCount: 1);
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.MarkReadAsync(Guid.NewGuid(), convId);

        Assert.False(result.IsSuccess);
        Assert.Contains("access denied", result.Error);
    }

    [Fact]
    public async Task MarkRead_NoMessages_SucceedsWithoutChangingLastReadId()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync(messageCount: 0);
        var svc = new ConversationService(ctx, Dtp());

        var result = await svc.MarkReadAsync(user1, convId);

        Assert.True(result.IsSuccess);
        var participant = ctx.ConversationParticipants
            .First(cp => cp.ConversationId == convId && cp.UserId == user1);
        Assert.Null(participant.LastReadMessageId);
    }

    [Fact]
    public async Task MarkRead_AlreadyUpToDate_DoesNotThrow()
    {
        var (ctx, convId, user1, _) = await SeedConversationAsync(messageCount: 2);
        var svc = new ConversationService(ctx, Dtp());

        // First mark
        var first = await svc.MarkReadAsync(user1, convId);
        Assert.True(first.IsSuccess);

        // Second mark - idempotent
        var second = await svc.MarkReadAsync(user1, convId);
        Assert.True(second.IsSuccess);
    }

    // ── SendMessageRequest validation ─────────────────────────────────────────

    [Fact]
    public void SendMessageRequest_ValidationAttributes_ArePresent()
    {
        var prop = typeof(SendMessageRequest).GetProperty(nameof(SendMessageRequest.Body));
        Assert.NotNull(prop);

        var attrs = prop!.GetCustomAttributes(false);
        Assert.Contains(attrs, a => a is System.ComponentModel.DataAnnotations.RequiredAttribute);
        Assert.Contains(attrs, a => a is System.ComponentModel.DataAnnotations.MaxLengthAttribute maxLen && maxLen.Length == 4000);
        Assert.Contains(attrs, a => a is System.ComponentModel.DataAnnotations.MinLengthAttribute minLen && minLen.Length == 1);
    }
}
