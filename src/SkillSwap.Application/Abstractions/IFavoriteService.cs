using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Favorites;

namespace SkillSwap.Application.Abstractions;

public interface IFavoriteService
{
    Task<Result<IReadOnlyList<FavoriteDto>>> GetFavoritesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<FavoriteDto>> AddFavoriteAsync(Guid userId, Guid favoriteUserId, CancellationToken cancellationToken = default);
    Task<Result> RemoveFavoriteAsync(Guid userId, Guid favoriteUserId, CancellationToken cancellationToken = default);
}
