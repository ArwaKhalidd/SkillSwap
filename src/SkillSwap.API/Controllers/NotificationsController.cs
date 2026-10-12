using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Notifications;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;

    public NotificationsController(
        INotificationService notificationService,
        ICurrentUserService currentUserService)
    {
        _notificationService = notificationService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool unreadOnly,
        CancellationToken cancellationToken)
    {
        var callerId = GetCurrentUserId();
        var result = await _notificationService.GetNotificationsAsync(callerId, unreadOnly, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{id:long}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken cancellationToken)
    {
        var callerId = GetCurrentUserId();
        var result = await _notificationService.MarkAsReadAsync(callerId, id, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error == "Notification not found.")
                return NotFound(result.Error);

            return BadRequest(result.Error);
        }

        return Ok();
    }

    private Guid GetCurrentUserId()
    {
        if (_currentUserService.UserId == null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        return _currentUserService.UserId.Value;
    }
}
