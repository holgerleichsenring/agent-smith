using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: renders the accounts as the text a reviewer reads.
/// <para>
/// Not a confidence score and not a hedge: the claim, itemised, each satisfied criterion
/// pointing at the file it is satisfied by. A reviewer opens one file to refute a line —
/// which is cheaper than re-deriving what the phase did, and honest about what was
/// checked mechanically and what was read off a diff.
/// </para>
/// </summary>
public static class SpecAccountRenderer
{
    /// <summary>p0429a: the heading is the caller's, because a scan accounts for what it
    /// LOOKED FOR and a phase for what it DELIVERS — one renderer, two readings.</summary>
    public static string ToMarkdown(
        IReadOnlyList<SpecAccount> accounts, string heading = "## What this phase delivers")
    {
        ArgumentNullException.ThrowIfNull(accounts);
        if (accounts.Count == 0) return string.Empty;

        var lines = new List<string> { heading, string.Empty };
        foreach (var account in accounts)
        {
            lines.Add($"**{account.RepoKey}**");
            if (account.Problem is not null)
            {
                lines.Add($"- no account could be taken — {account.Problem}");
                lines.Add(string.Empty);
                continue;
            }
            lines.AddRange(account.Criteria.Select(Row));
            lines.Add(string.Empty);
        }
        return string.Join("\n", lines).TrimEnd() + "\n";
    }

    private static string Row(CriterionAccount criterion)
    {
        var mark = criterion.Satisfied ? "x" : " ";
        var evidence = criterion switch
        {
            { Satisfied: true, Mechanical: true } => " — verified by command",
            { Satisfied: true, Citation: not null } => $" — `{criterion.Citation}`",
            { Note: not null } => $" — {criterion.Note}",
            _ => string.Empty,
        };
        return $"- [{mark}] {criterion.Criterion}{evidence}";
    }
}
