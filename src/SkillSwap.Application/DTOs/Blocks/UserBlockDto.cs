namespace SkillSwap.Application.DTOs.Blocks;

public record UserBlockDto(
    Guid BlockerId,
    Guid BlockedId,
    DateTime CreatedAtUtc);
