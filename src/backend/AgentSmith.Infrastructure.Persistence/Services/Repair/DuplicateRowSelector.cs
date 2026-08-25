using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Infrastructure.Persistence.Services.Repair;

/// <summary>
/// 2026-08-25-61f1: decides which rows of a duplicate set are the copies. The earliest
/// written row survives — duplicates of one event carry insert stamps seconds apart because
/// each replay stamped its own, and the first one is the one that happened. Row id breaks a
/// tie so the answer is the same on every provider and every run of the repair.
/// </summary>
public sealed class DuplicateRowSelector
{
    public IReadOnlyList<long> Superfluous(IEnumerable<RepairRow> rows) =>
        [.. rows.GroupBy(row => row.Key, StringComparer.Ordinal)
            .Where(set => set.Count() > 1)
            .SelectMany(set => set.OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).Skip(1))
            .Select(row => row.Id)];
}
