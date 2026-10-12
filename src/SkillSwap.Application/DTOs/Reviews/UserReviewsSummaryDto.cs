namespace SkillSwap.Application.DTOs.Reviews;

public record UserReviewsSummaryDto(
    Guid UserId,
    double AverageRating,
    int TotalReviews,
    IReadOnlyList<ReviewDto> Reviews);
