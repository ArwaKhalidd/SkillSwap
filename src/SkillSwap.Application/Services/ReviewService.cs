using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Reviews;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReviewService(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ReviewDto>> CreateReviewAsync(
        Guid reviewerId,
        CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Rating < 1 || request.Rating > 5)
        {
            return Result.Failure<ReviewDto>("Rating must be between 1 and 5.");
        }

        if (request.Comment?.Length > 1000)
        {
            return Result.Failure<ReviewDto>("Comment cannot exceed 1000 characters.");
        }

        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result.Failure<ReviewDto>("Session not found.");
        }

        if (session.Status != SessionStatus.Completed)
        {
            return Result.Failure<ReviewDto>("Reviews can only be submitted for completed sessions.");
        }

        if (session.LearnerId != reviewerId && session.TeacherId != reviewerId)
        {
            return Result.Failure<ReviewDto>("Only session participants can submit a review.");
        }

        var revieweeId = reviewerId == session.LearnerId ? session.TeacherId : session.LearnerId;

        if (reviewerId == revieweeId)
        {
            return Result.Failure<ReviewDto>("Cannot review yourself.");
        }

        var alreadyReviewed = await _context.Reviews
            .AnyAsync(r => r.SessionId == session.Id && r.ReviewerId == reviewerId, cancellationToken);

        if (alreadyReviewed)
        {
            return Result.Failure<ReviewDto>("A review has already been submitted for this session.");
        }

        var review = new Review
        {
            SessionId = session.Id,
            ReviewerId = reviewerId,
            RevieweeId = revieweeId,
            Rating = request.Rating,
            Comment = request.Comment?.Trim(),
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.Reviews.Add(review);

        var notification = new Notification
        {
            UserId = revieweeId,
            Type = NotificationKind.ReviewReceived,
            Payload = $"{{\"sessionId\":{session.Id},\"rating\":{request.Rating}}}",
            IsRead = false,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new ReviewDto(
            review.Id,
            review.SessionId,
            review.ReviewerId,
            review.RevieweeId,
            review.Rating,
            review.Comment,
            review.CreatedAtUtc));
    }

    public async Task<Result<UserReviewsSummaryDto>> GetReviewsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var reviews = await _context.Reviews
            .AsNoTracking()
            .Where(r => r.RevieweeId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ReviewDto(
                r.Id,
                r.SessionId,
                r.ReviewerId,
                r.RevieweeId,
                r.Rating,
                r.Comment,
                r.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var totalCount = reviews.Count;
        var avgRating = totalCount > 0 ? Math.Round(reviews.Average(r => (double)r.Rating), 2) : 0.0;

        return Result.Success(new UserReviewsSummaryDto(userId, avgRating, totalCount, reviews));
    }
}
