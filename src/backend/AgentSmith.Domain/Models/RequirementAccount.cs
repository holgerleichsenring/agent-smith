namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-3c12: what this scan can say about the requirements of the published
/// standard, per entry group and per station of a request.
/// <para>
/// It is a REPORTING surface, exactly like the entry map it stands on. It is rendered
/// beside the findings and raises findings of its own, and it never reaches the ledger the
/// delivery gate reads: an entry undecidable for want of an input the scan was never given
/// would be outstanding forever, and gating on it would fail every scan of every repository
/// that ships without a threat model.
/// </para>
/// <para>
/// <paramref name="CatalogueVersion"/> travels with it because the standard's current
/// release renumbered its predecessor: an id without the version that issued it cites
/// nothing. <paramref name="Attribution"/> is the licence line the ingested text carries.
/// </para>
/// </summary>
public sealed record RequirementAccount(
    string CatalogueVersion,
    string Attribution,
    IReadOnlyList<EntryGroupRequirements> Groups)
{
    public static RequirementAccount Empty { get; } = new(string.Empty, string.Empty, []);

    public bool IsEmpty => Groups.Count == 0;

    /// <summary>Every row the standard was asked, across every attempted group.</summary>
    public IReadOnlyList<RequirementRow> Rows => [.. Groups.SelectMany(g => g.Rows)];

    /// <summary>The groups the run never reached — a budget fact, not a verdict.</summary>
    public IReadOnlyList<EntryGroupRequirements> NotAttempted =>
        [.. Groups.Where(g => !g.Attempted)];

    public int AnsweredCount => Rows.Count(r => r.Answered);
}
