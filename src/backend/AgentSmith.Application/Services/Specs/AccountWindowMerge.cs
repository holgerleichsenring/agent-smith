using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: merges the answers of several diff windows into one account.
/// <para>
/// Evidence is MONOTONE: what a window shows, it shows, whatever the other windows
/// contain. So the first window that satisfies a criterion wins it, and a window that
/// could not see the evidence is a statement about that slice, never about the branch.
/// </para>
/// <para>
/// 2026-08-25-9749: with three dispositions the rule has to be a RANKING or it becomes
/// window-order-dependent. It is <see cref="AccountDisposition"/>'s own order — satisfied
/// over not applicable over not satisfied — and the FIRST row at the winning rank is kept,
/// so the answer does not depend on how the diff happened to be cut.
/// </para>
/// </summary>
public static class AccountWindowMerge
{
    public static IReadOnlyList<AccountRow> Of(IEnumerable<IReadOnlyList<AccountRow>> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        var merged = new Dictionary<string, AccountRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in windows.SelectMany(rows => rows))
        {
            if (merged.TryGetValue(row.Criterion, out var kept)
                && kept.Disposition >= row.Disposition) continue;
            merged[row.Criterion] = row;
        }
        return [.. merged.Values];
    }
}
