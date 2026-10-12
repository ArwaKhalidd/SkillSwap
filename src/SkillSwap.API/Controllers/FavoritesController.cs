using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Favorites;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FavoritesController : ControllerBase
{
    private readonly IFavoriteService _favoriteService;
    private readonly ICurrentUserService _currentUserService;

    public FavoritesController(
        IFavoriteService favoriteService,
        ICurrentUserService currentUserService)
    {
        _favoriteService = favoriteService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FavoriteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFavorites(CancellationToken cancellationToken)
    {
        var callerId = GetCurrentUserId();
        var result = await _favoriteService.GetFavoritesAsync(callerId, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{userId:guid}")]
    [ProducesResponseType(typeof(FavoriteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddFavorite(Guid userId, CancellationToken cancellationToken)
    {
        var callerId = GetCurrentUserId();
        var result = await _favoriteService.AddFavoriteAsync(callerId, userId, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveFavorite(Guid userId, CancellationToken cancellationToken)
    {
        var callerId = GetCurrentUserId();
        var result = await _favoriteService.RemoveFavoriteAsync(callerId, userId, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error == "Favorite not found.")
                return NotFound(result.Error);

            return BadRequest(result.Error);
        }

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        if (_currentUserService.UserId == null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        return _currentUserService.UserId.Value;
    }
}
