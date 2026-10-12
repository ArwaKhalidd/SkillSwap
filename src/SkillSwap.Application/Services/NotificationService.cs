using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Notifications;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public NotificationService(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<NotificationDto>>> GetNotificationsAsync(
        Guid userId,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NotificationDto(
                n.Id,
                n.UserId,
                n.Type,
                n.Payload,
                n.IsRead,
                n.ReadAtUtc,
                n.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<NotificationDto>>(items);
    }

    public async Task<Result> MarkAsReadAsync(
        Guid userId,
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

        if (notification == null)
        {
            return Result.Failure("Notification not found.");
        }

        if (notification.UserId != userId)
        {
            return Result.Failure("Unauthorized access to notification.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = _dateTimeProvider.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<NotificationDto>> CreateNotificationAsync(
        Guid userId,
        NotificationKind type,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Payload = payload,
            IsRead = false,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new NotificationDto(
            notification.Id,
            notification.UserId,
            notification.Type,
            notification.Payload,
            notification.IsRead,
            notification.ReadAtUtc,
            notification.CreatedAtUtc));
    }

    public async Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }
}
