using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Subscriptions;

namespace SkillSwap.Application.Abstractions;

public interface ISubscriptionService
{
    Task<Result<IReadOnlyList<SubscriptionPlanDto>>> GetPlansAsync(CancellationToken cancellationToken = default);
    Task<Result<UserSubscriptionDto>> GetActiveSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<UserSubscriptionDto>> SubscribeAsync(Guid userId, int planId, CancellationToken cancellationToken = default);
    Task<int> GetMonthlyLearningMinutesCapAsync(Guid userId, CancellationToken cancellationToken = default);
}
