namespace SkillSwap.Application.DTOs.Favorites;

public record FavoriteDto(
    Guid UserId,
    Guid FavoriteUserId,
    DateTime CreatedAtUtc);
