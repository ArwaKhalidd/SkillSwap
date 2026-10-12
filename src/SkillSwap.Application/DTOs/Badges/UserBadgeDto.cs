namespace SkillSwap.Application.DTOs.Badges;

public record UserBadgeDto(
    Guid UserId,
    int BadgeId,
    string Code,
    string Name,
    string Description,
    string? IconUrl,
    DateTime AwardedAtUtc);
