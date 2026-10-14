using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Conversations;

namespace SkillSwap.API.Controllers;

/// <summary>
/// Provides REST endpoints for conversations and in-conversation messaging.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;
    private readonly ICurrentUserService _currentUserService;

    public ConversationsController(
        IConversationService conversationService,
        ICurrentUserService currentUserService)
    {
        _conversationService = conversationService;
        _currentUserService = currentUserService;
    }

    // GET /api/conversations
    /// <summary>
    /// Returns all conversations for the authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var callerId = GetCurrentUserId();
        var result = await _conversationService.GetConversationsAsync(callerId, cancellationToken);
        return Ok(result.Value);
    }

    // GET /api/conversations/{id}/messages?pageSize=20&beforeMessageId=...
    /// <summary>
    /// Returns messages in the specified conversation, newest first with cursor pagination.
    /// </summary>
    [HttpGet("{id:long}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMessages(
        long id,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? beforeMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var callerId = GetCurrentUserId();
        var result = await _conversationService.GetMessagesAsync(
            callerId, id, pageSize, beforeMessageId, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("access denied"))
                return StatusCode(StatusCodes.Status403Forbidden, result.Error);

            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    // POST /api/conversations/{id}/messages
    /// <summary>
    /// Sends a new message in the specified conversation.
    /// </summary>
    [HttpPost("{id:long}/messages")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendMessage(
        long id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var callerId = GetCurrentUserId();
        var result = await _conversationService.SendMessageAsync(callerId, id, request, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("access denied"))
                return StatusCode(StatusCodes.Status403Forbidden, result.Error);

            return BadRequest(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    // POST /api/conversations/{id}/read
    /// <summary>
    /// Marks all messages in the conversation as read for the authenticated user.
    /// </summary>
    [HttpPost("{id:long}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkRead(
        long id,
        CancellationToken cancellationToken = default)
    {
        var callerId = GetCurrentUserId();
        var result = await _conversationService.MarkReadAsync(callerId, id, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Contains("access denied"))
                return StatusCode(StatusCodes.Status403Forbidden, result.Error);

            return BadRequest(result.Error);
        }

        return Ok();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        if (_currentUserService.UserId == null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        return _currentUserService.UserId.Value;
    }
}
