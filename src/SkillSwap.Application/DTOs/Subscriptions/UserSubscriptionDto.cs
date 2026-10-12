using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Subscriptions;

public record UserSubscriptionDto(
    long Id,
    Guid UserId,
    int PlanId,
    string PlanCode,
    string PlanName,
    int MonthlyLearningMinutes,
    decimal? Price,
    bool PriorityMatching,
    bool AdvancedSearch,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    SubscriptionStatus Status,
    DateTime CreatedAtUtc);
