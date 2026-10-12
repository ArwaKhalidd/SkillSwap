using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Reports;

namespace SkillSwap.Application.Abstractions;

public interface IReportService
{
    Task<Result<ReportDto>> CreateReportAsync(Guid reporterId, CreateReportRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ReportDto>>> GetReportsAsync(Guid? reporterId = null, CancellationToken cancellationToken = default);
}
