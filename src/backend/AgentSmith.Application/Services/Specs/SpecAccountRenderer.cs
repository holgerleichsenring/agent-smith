using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: renders the accounts as the text a reviewer reads.
/// <para>
/// Not a confidence score and not a hedge: the claim, itemised, each satisfied criterion
/// pointing at the file it is satisfied by. A reviewer opens one file to refute a line —
/// which is cheaper than re-deriving what the spec did, and honest about what was
/// checked mechanically and what was read off a diff.
/// </para>
/// </summary>
public static class SpecAccountRenderer
{
    /// <summary>p0429a: the heading is the caller's, because a scan accounts for what it
    /// LOOKED FOR and a spec for what it DELIVERS — one renderer, two readings.</summary>
    public static string ToMarkdown(
        IReadOnlyList<SpecAccount> accounts, string heading = "## What this spec delivers")
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
            lines.AddRange(Report(account)); // 2026-11-18-c8e2d
            if (account.Measures is { } measures) lines.AddRange(Measured(measures));
            lines.Add(string.Empty);
        }
        return string.Join("\n", lines).TrimEnd() + "\n";
    }

    /// <summary>
    /// 2026-09-01-3653: how large the prompt was, how many turns it used against its ceiling
    /// and how much of the source it read — beside the criteria those numbers explain. The
    /// turn count is rendered as near-exact because that is what it is.
    /// </summary>
    private static IEnumerable<string> Measured(ScanPassMeasures measures) =>
    [
        $"- measured: system prompt {measures.SystemPromptChars} chars; conversation "
        + $"{measures.ConversationChars}; scanner findings {measures.ScannerFindingsChars}; "
        + $"OpenAPI document {measures.OpenApiDocumentChars}; surface difference "
        + $"{measures.SurfaceDifferenceChars}",
        $"- measured: ~{measures.TurnsUsed} turns against a ceiling of "
        + $"{measures.IterationCeiling} (near-exact — counted from the pass's assistant "
        + $"messages, and a provider may split one turn across several); "
        + $"{measures.DistinctReadCount} distinct source file(s) read",
    ];

    /// <summary>2026-11-18-c8e2d: the rest of the master's report — fixed in passing and the follow-ups it proposes.</summary>
    private static IEnumerable<string> Report(SpecAccount account) =>
    [
        // 2026-11-18-98fa: the master's own markdown, as written — the record and the pull request show it whole.
        .. string.IsNullOrWhiteSpace(account.Report) ? [] : new[] { string.Empty, account.Report.Trim(), string.Empty },
        .. (account.FixedInPassing ?? []).Select(f => $"Fixed in passing: {f}"),
        .. (account.FollowUps ?? []).Select(f => $"Proposed follow-up: {f.Goal}" + (string.IsNullOrWhiteSpace(f.Why) ? "" : $" — {f.Why}")),
    ];

    private static string Row(CriterionAccount criterion) =>
        $"- [{Mark(criterion.Disposition)}] {criterion.Criterion}{Evidence(criterion)}";

    /// <summary>2026-08-25-9749: three marks, because three dispositions. A declined
    /// criterion reading as an empty box would be indistinguishable from one the branch
    /// failed, which is the whole defect this spec exists to end.</summary>
    private static string Mark(AccountDisposition disposition) => disposition switch
    {
        AccountDisposition.Satisfied => "x",
        AccountDisposition.NotApplicable => "~",
        _ => " ",
    };

    private static string Evidence(CriterionAccount criterion) => criterion switch
    {
        { Disposition: AccountDisposition.Satisfied, Mechanical: true } => " — verified by command",
        { Disposition: AccountDisposition.Satisfied, Citation: not null } => $" — `{criterion.Citation}`",
        // 2026-11-18-c8e2d: a master's row says its own why; a scan's names the antecedent its search disproved.
        { Disposition: AccountDisposition.NotApplicable, Antecedent: null, Note: not null } => $" — not applicable: {criterion.Note}",
        { Disposition: AccountDisposition.NotApplicable } =>
            $" — not applicable: the base carries no {criterion.Antecedent}"
            + (criterion.Citation is null ? string.Empty : $" (`{criterion.Citation}`)"),
        { Note: not null } => $" — {criterion.Note}",
        _ => string.Empty,
    };
}
