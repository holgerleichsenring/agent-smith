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

    private static string Row(CriterionAccount criterion) =>
        $"- [{Mark(criterion.Disposition)}] {criterion.Criterion}{Evidence(criterion)}";

    /// <summary>2026-08-25-9749: three marks, because three dispositions. A declined
    /// criterion reading as an empty box would be indistinguishable from one the branch
    /// failed, which is the whole defect this phase exists to end.</summary>
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
        { Disposition: AccountDisposition.NotApplicable } =>
            $" — not applicable: the base carries no {criterion.Antecedent}"
            + (criterion.Citation is null ? string.Empty : $" (`{criterion.Citation}`)"),
        { Note: not null } => $" — {criterion.Note}",
        _ => string.Empty,
    };
}
