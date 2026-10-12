using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Notifications;

public record NotificationDto(
    long Id,
    Guid UserId,
    NotificationKind Type,
    string Payload,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc);
