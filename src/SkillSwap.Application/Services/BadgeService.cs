using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Badges;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public class BadgeService : IBadgeService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BadgeService(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<BadgeDto>>> GetAllBadgesAsync(CancellationToken cancellationToken = default)
    {
        var badges = await _context.Badges
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new BadgeDto(
                b.Id,
                b.Code,
                b.Name,
                b.Description,
                b.IconUrl))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BadgeDto>>(badges);
    }

    public async Task<Result<IReadOnlyList<UserBadgeDto>>> GetBadgesForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var userBadges = await _context.UserBadges
            .AsNoTracking()
            .Include(ub => ub.Badge)
            .Where(ub => ub.UserId == userId)
            .OrderByDescending(ub => ub.AwardedAtUtc)
            .Select(ub => new UserBadgeDto(
                ub.UserId,
                ub.BadgeId,
                ub.Badge.Code,
                ub.Badge.Name,
                ub.Badge.Description,
                ub.Badge.IconUrl,
                ub.AwardedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<UserBadgeDto>>(userBadges);
    }

    public async Task<Result<UserBadgeDto>> AwardBadgeAsync(
        Guid userId,
        string badgeCode,
        CancellationToken cancellationToken = default)
    {
        var badge = await _context.Badges
            .FirstOrDefaultAsync(b => b.Code.ToLower() == badgeCode.ToLower(), cancellationToken);

        if (badge == null)
        {
            return Result.Failure<UserBadgeDto>($"Badge with code '{badgeCode}' was not found.");
        }

        var existing = await _context.UserBadges
            .Include(ub => ub.Badge)
            .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BadgeId == badge.Id, cancellationToken);

        if (existing != null)
        {
            return Result.Success(new UserBadgeDto(
                existing.UserId,
                existing.BadgeId,
                existing.Badge.Code,
                existing.Badge.Name,
                existing.Badge.Description,
                existing.Badge.IconUrl,
                existing.AwardedAtUtc));
        }

        var userBadge = new UserBadge
        {
            UserId = userId,
            BadgeId = badge.Id,
            AwardedAtUtc = _dateTimeProvider.UtcNow,
            Badge = badge
        };

        _context.UserBadges.Add(userBadge);

        var notification = new Notification
        {
            UserId = userId,
            Type = NotificationKind.BadgeEarned,
            Payload = $"{{\"badgeCode\":\"{badge.Code}\",\"badgeName\":\"{badge.Name}\"}}",
            IsRead = false,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserBadgeDto(
            userId,
            badge.Id,
            badge.Code,
            badge.Name,
            badge.Description,
            badge.IconUrl,
            userBadge.AwardedAtUtc));
    }
}
