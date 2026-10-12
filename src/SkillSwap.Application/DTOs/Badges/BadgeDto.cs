namespace SkillSwap.Application.DTOs.Badges;

public record BadgeDto(
    int Id,
    string Code,
    string Name,
    string Description,
    string? IconUrl);
