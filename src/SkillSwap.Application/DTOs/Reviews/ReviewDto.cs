namespace SkillSwap.Application.DTOs.Reviews;

public record ReviewDto(
    long Id,
    long SessionId,
    Guid ReviewerId,
    Guid RevieweeId,
    byte Rating,
    string? Comment,
    DateTime CreatedAtUtc);
