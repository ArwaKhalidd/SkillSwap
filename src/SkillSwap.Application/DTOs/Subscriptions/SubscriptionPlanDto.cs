namespace SkillSwap.Application.DTOs.Subscriptions;

public record SubscriptionPlanDto(
    int Id,
    string Code,
    string Name,
    int MonthlyLearningMinutes,
    decimal? Price,
    bool PriorityMatching,
    bool AdvancedSearch,
    bool IsActive);
