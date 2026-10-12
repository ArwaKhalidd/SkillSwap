using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Blocks;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Application.Services;

public class BlockService : IBlockService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BlockService(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<UserBlockDto>> BlockUserAsync(
        Guid blockerId,
        Guid blockedId,
        CancellationToken cancellationToken = default)
    {
        if (blockerId == blockedId)
        {
            return Result.Failure<UserBlockDto>("Cannot block yourself.");
        }

        var existing = await _context.UserBlocks
            .FirstOrDefaultAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, cancellationToken);

        if (existing != null)
        {
            return Result.Success(new UserBlockDto(existing.BlockerId, existing.BlockedId, existing.CreatedAtUtc));
        }

        var block = new UserBlock
        {
            BlockerId = blockerId,
            BlockedId = blockedId,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.UserBlocks.Add(block);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserBlockDto(block.BlockerId, block.BlockedId, block.CreatedAtUtc));
    }

    public async Task<Result> UnblockUserAsync(
        Guid blockerId,
        Guid blockedId,
        CancellationToken cancellationToken = default)
    {
        var block = await _context.UserBlocks
            .FirstOrDefaultAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, cancellationToken);

        if (block == null)
        {
            return Result.Failure("Block record not found.");
        }

        _context.UserBlocks.Remove(block);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<bool> IsBlockedAsync(
        Guid userA,
        Guid userB,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserBlocks
            .AsNoTracking()
            .AnyAsync(b => (b.BlockerId == userA && b.BlockedId == userB) ||
                           (b.BlockerId == userB && b.BlockedId == userA), cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetBlockedUserIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserBlocks
            .AsNoTracking()
            .Where(b => b.BlockerId == userId)
            .Select(b => b.BlockedId)
            .ToListAsync(cancellationToken);
    }
}
