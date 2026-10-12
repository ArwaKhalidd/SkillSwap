namespace SkillSwap.Application.DTOs.Reviews;

public record CreateReviewRequest(
    long SessionId,
    byte Rating,
    string? Comment);
