using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Favorites;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Application.Services;

public class FavoriteService : IFavoriteService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public FavoriteService(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<FavoriteDto>>> GetFavoritesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var favorites = await _context.Favorites
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Select(f => new FavoriteDto(f.UserId, f.FavoriteUserId, f.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<FavoriteDto>>(favorites);
    }

    public async Task<Result<FavoriteDto>> AddFavoriteAsync(
        Guid userId,
        Guid favoriteUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId == favoriteUserId)
        {
            return Result.Failure<FavoriteDto>("Cannot favorite yourself.");
        }

        var existing = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.FavoriteUserId == favoriteUserId, cancellationToken);

        if (existing != null)
        {
            return Result.Success(new FavoriteDto(existing.UserId, existing.FavoriteUserId, existing.CreatedAtUtc));
        }

        var favorite = new Favorite
        {
            UserId = userId,
            FavoriteUserId = favoriteUserId,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.Favorites.Add(favorite);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new FavoriteDto(favorite.UserId, favorite.FavoriteUserId, favorite.CreatedAtUtc));
    }

    public async Task<Result> RemoveFavoriteAsync(
        Guid userId,
        Guid favoriteUserId,
        CancellationToken cancellationToken = default)
    {
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.FavoriteUserId == favoriteUserId, cancellationToken);

        if (favorite == null)
        {
            return Result.Failure("Favorite not found.");
        }

        _context.Favorites.Remove(favorite);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
