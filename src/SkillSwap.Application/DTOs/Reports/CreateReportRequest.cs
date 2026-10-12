using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Reports;

public record CreateReportRequest(
    Guid ReportedUserId,
    ReportReason Reason,
    string? Description,
    long? SessionId = null);
