using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Blocks;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BlocksController : ControllerBase
{
    private readonly IBlockService _blockService;
    private readonly ICurrentUserService _currentUserService;

    public BlocksController(
        IBlockService blockService,
        ICurrentUserService currentUserService)
    {
        _blockService = blockService;
        _currentUserService = currentUserService;
    }

    [HttpPost("{userId:guid}")]
    [ProducesResponseType(typeof(UserBlockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BlockUser(Guid userId, CancellationToken cancellationToken)
    {
        var callerId = GetCurrentUserId();
        var result = await _blockService.BlockUserAsync(callerId, userId, cancellationToken);

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
    public async Task<IActionResult> UnblockUser(Guid userId, CancellationToken cancellationToken)
    {
        var callerId = GetCurrentUserId();
        var result = await _blockService.UnblockUserAsync(callerId, userId, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error == "Block record not found.")
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
