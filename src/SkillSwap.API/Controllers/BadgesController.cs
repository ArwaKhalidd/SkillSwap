using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Badges;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BadgesController : ControllerBase
{
    private readonly IBadgeService _badgeService;

    public BadgesController(IBadgeService badgeService)
    {
        _badgeService = badgeService;
    }

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BadgeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllBadges(CancellationToken cancellationToken)
    {
        var result = await _badgeService.GetAllBadgesAsync(cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("~/api/users/{id:guid}/badges")]
    [ProducesResponseType(typeof(IReadOnlyList<UserBadgeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserBadges(Guid id, CancellationToken cancellationToken)
    {
        var result = await _badgeService.GetBadgesForUserAsync(id, cancellationToken);
        return Ok(result.Value);
    }
}
