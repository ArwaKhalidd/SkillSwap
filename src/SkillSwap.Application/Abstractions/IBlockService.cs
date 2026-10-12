using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Blocks;

namespace SkillSwap.Application.Abstractions;

public interface IBlockService
{
    Task<Result<UserBlockDto>> BlockUserAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default);
    Task<Result> UnblockUserAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default);
    Task<bool> IsBlockedAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetBlockedUserIdsAsync(Guid userId, CancellationToken cancellationToken = default);
}
