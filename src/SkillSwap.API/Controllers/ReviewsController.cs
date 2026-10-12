using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Reviews;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ICurrentUserService _currentUserService;

    public ReviewsController(IReviewService reviewService, ICurrentUserService currentUserService)
    {
        _reviewService = reviewService;
        _currentUserService = currentUserService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var callerId = GetCurrentUserId();
        var result = await _reviewService.CreateReviewAsync(callerId, request, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [AllowAnonymous]
    [HttpGet("~/api/users/{id:guid}/reviews")]
    [ProducesResponseType(typeof(UserReviewsSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserReviews(Guid id, CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetReviewsForUserAsync(id, cancellationToken);
        return Ok(result.Value);
    }

    private Guid GetCurrentUserId()
    {
        if (_currentUserService.UserId == null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        return _currentUserService.UserId.Value;
    }
}
