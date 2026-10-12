using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Reports;

public record ReportDto(
    long Id,
    Guid ReporterId,
    Guid ReportedUserId,
    long? SessionId,
    ReportReason Reason,
    string? Description,
    ReportStatus Status,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc);
