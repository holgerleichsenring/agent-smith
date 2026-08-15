namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: merges the answers of several diff windows into one account.
/// <para>
/// Evidence is MONOTONE: what a window shows, it shows, whatever the other windows
/// contain. So the first window that satisfies a criterion wins it, and a window that
/// could not see the evidence is a statement about that slice, never about the branch.
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
            if (merged.TryGetValue(row.Criterion, out var kept) && kept.Satisfied) continue;
            merged[row.Criterion] = row;
        }
        return [.. merged.Values];
    }
}
