using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Subscriptions;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public const int DefaultFreeMinutes = 180;

    public SubscriptionService(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<SubscriptionPlanDto>>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _context.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .Select(p => new SubscriptionPlanDto(
                p.Id,
                p.Code,
                p.Name,
                p.MonthlyLearningMinutes,
                p.Price,
                p.PriorityMatching,
                p.AdvancedSearch,
                p.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SubscriptionPlanDto>>(plans);
    }

    public async Task<Result<UserSubscriptionDto>> GetActiveSubscriptionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _context.UserSubscriptions
            .Include(s => s.Plan)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, cancellationToken);

        if (subscription?.Plan != null)
        {
            return Result.Success(new UserSubscriptionDto(
                subscription.Id,
                subscription.UserId,
                subscription.PlanId,
                subscription.Plan.Code,
                subscription.Plan.Name,
                subscription.Plan.MonthlyLearningMinutes,
                subscription.Plan.Price,
                subscription.Plan.PriorityMatching,
                subscription.Plan.AdvancedSearch,
                subscription.StartsAtUtc,
                subscription.EndsAtUtc,
                subscription.Status,
                subscription.CreatedAtUtc));
        }

        // Fallback to default Free plan contract
        var freePlan = await _context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == "FREE", cancellationToken);

        return Result.Success(new UserSubscriptionDto(
            0,
            userId,
            freePlan?.Id ?? 1,
            freePlan?.Code ?? "FREE",
            freePlan?.Name ?? "Free Plan",
            freePlan?.MonthlyLearningMinutes ?? DefaultFreeMinutes,
            0.00m,
            false,
            false,
            _dateTimeProvider.UtcNow,
            null,
            SubscriptionStatus.Active,
            _dateTimeProvider.UtcNow));
    }

    public async Task<Result<UserSubscriptionDto>> SubscribeAsync(
        Guid userId,
        int planId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, cancellationToken);

        if (plan == null)
        {
            return Result.Failure<UserSubscriptionDto>("Subscription plan not found or inactive.");
        }

        var activeSubscription = await _context.UserSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, cancellationToken);

        if (activeSubscription != null)
        {
            if (activeSubscription.PlanId == plan.Id)
            {
                return Result.Success(new UserSubscriptionDto(
                    activeSubscription.Id,
                    activeSubscription.UserId,
                    activeSubscription.PlanId,
                    activeSubscription.Plan.Code,
                    activeSubscription.Plan.Name,
                    activeSubscription.Plan.MonthlyLearningMinutes,
                    activeSubscription.Plan.Price,
                    activeSubscription.Plan.PriorityMatching,
                    activeSubscription.Plan.AdvancedSearch,
                    activeSubscription.StartsAtUtc,
                    activeSubscription.EndsAtUtc,
                    activeSubscription.Status,
                    activeSubscription.CreatedAtUtc));
            }

            activeSubscription.Status = SubscriptionStatus.Cancelled;
            activeSubscription.EndsAtUtc = _dateTimeProvider.UtcNow;
        }

        var newSubscription = new UserSubscription
        {
            UserId = userId,
            PlanId = plan.Id,
            StartsAtUtc = _dateTimeProvider.UtcNow,
            EndsAtUtc = null,
            Status = SubscriptionStatus.Active,
            CreatedAtUtc = _dateTimeProvider.UtcNow,
            Plan = plan
        };

        _context.UserSubscriptions.Add(newSubscription);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserSubscriptionDto(
            newSubscription.Id,
            newSubscription.UserId,
            newSubscription.PlanId,
            plan.Code,
            plan.Name,
            plan.MonthlyLearningMinutes,
            plan.Price,
            plan.PriorityMatching,
            plan.AdvancedSearch,
            newSubscription.StartsAtUtc,
            newSubscription.EndsAtUtc,
            newSubscription.Status,
            newSubscription.CreatedAtUtc));
    }

    public async Task<int> GetMonthlyLearningMinutesCapAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _context.UserSubscriptions
            .Include(s => s.Plan)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, cancellationToken);

        return subscription?.Plan?.MonthlyLearningMinutes ?? DefaultFreeMinutes;
    }
}
