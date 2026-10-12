using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Subscriptions;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ICurrentUserService _currentUserService;

    public SubscriptionsController(
        ISubscriptionService subscriptionService,
        ICurrentUserService currentUserService)
    {
        _subscriptionService = subscriptionService;
        _currentUserService = currentUserService;
    }

    [AllowAnonymous]
    [HttpGet("plans")]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.GetPlansAsync(cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(UserSubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySubscription(CancellationToken cancellationToken)
    {
        var callerId = GetCurrentUserId();
        var result = await _subscriptionService.GetActiveSubscriptionAsync(callerId, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("subscribe")]
    [ProducesResponseType(typeof(UserSubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request, CancellationToken cancellationToken)
    {
        var callerId = GetCurrentUserId();
        var result = await _subscriptionService.SubscribeAsync(callerId, request.PlanId, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    private Guid GetCurrentUserId()
    {
        if (_currentUserService.UserId == null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        return _currentUserService.UserId.Value;
    }
}
