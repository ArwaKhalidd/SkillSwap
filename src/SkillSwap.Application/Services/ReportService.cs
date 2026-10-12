using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Reports;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public class ReportService : IReportService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReportService(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ReportDto>> CreateReportAsync(
        Guid reporterId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (reporterId == request.ReportedUserId)
        {
            return Result.Failure<ReportDto>("Cannot report yourself.");
        }

        if (!Enum.IsDefined(typeof(ReportReason), request.Reason))
        {
            return Result.Failure<ReportDto>("Invalid report reason.");
        }

        if (request.Description?.Length > 2000)
        {
            return Result.Failure<ReportDto>("Description cannot exceed 2000 characters.");
        }

        var report = new Report
        {
            ReporterId = reporterId,
            ReportedUserId = request.ReportedUserId,
            SessionId = request.SessionId,
            Reason = request.Reason,
            Description = request.Description?.Trim(),
            Status = ReportStatus.Open,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new ReportDto(
            report.Id,
            report.ReporterId,
            report.ReportedUserId,
            report.SessionId,
            report.Reason,
            report.Description,
            report.Status,
            report.CreatedAtUtc,
            report.ResolvedAtUtc));
    }

    public async Task<Result<IReadOnlyList<ReportDto>>> GetReportsAsync(
        Guid? reporterId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Reports.AsNoTracking();

        if (reporterId.HasValue)
        {
            query = query.Where(r => r.ReporterId == reporterId.Value);
        }

        var reports = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ReportDto(
                r.Id,
                r.ReporterId,
                r.ReportedUserId,
                r.SessionId,
                r.Reason,
                r.Description,
                r.Status,
                r.CreatedAtUtc,
                r.ResolvedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ReportDto>>(reports);
    }
}
