using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>How a ratified criterion found its disposition.</summary>
public enum PairedBy
{
    /// <summary>The disposition named this criterion's text.</summary>
    Name,
    /// <summary>The disposition named nothing that matches and sat at this criterion's
    /// position — the pairing every answer got before 2026-09-06-9f14.</summary>
    Position,
    /// <summary>No disposition reached this criterion at all.</summary>
    Nothing,
}

/// <summary>One ratified criterion and the disposition the master's answer gave it, if any.</summary>
public sealed record CriterionPairing(
    int Position, string Criterion, AcceptanceDisposition? Disposition, PairedBy By)
{
    /// <summary>Met, or not-applicable with an evaluated reason; a missing disposition never satisfies.</summary>
    public bool Satisfied => Disposition is not null && Satisfies(Disposition);

    private static bool Satisfies(AcceptanceDisposition d) =>
        d.Status == AcceptanceStatus.Met
        || (d.Status == AcceptanceStatus.NotApplicable && !string.IsNullOrWhiteSpace(d.Evidence));
}

/// <summary>
/// 2026-09-06-9f14: the acceptance gate's answer together with the pairing it stands on, so
/// a trail reader sees WHICH criterion each disposition was measured against and whether the
/// name or the position decided it. A refusal is a verdict-level stop taken before any pairing.
/// </summary>
public sealed record AcceptanceJudgement(
    bool Satisfied, IReadOnlyList<CriterionPairing> Pairings, string? Refusal)
{
    public static AcceptanceJudgement Refused(string reason) => new(false, [], reason);

    public static AcceptanceJudgement Of(IReadOnlyList<CriterionPairing> pairings) =>
        new(pairings.All(p => p.Satisfied), pairings, null);

    /// <summary>One line for the run log: the count, then every criterion with its status
    /// and the pairing that produced it.</summary>
    public string Describe()
    {
        if (Refusal is not null) return $"contract not satisfied — {Refusal}";
        if (Pairings.Count == 0) return "no ratified criteria";
        var satisfied = Pairings.Count(p => p.Satisfied);
        return $"{satisfied} of {Pairings.Count} criteria satisfied: "
            + string.Join("; ", Pairings.Select(Row));
    }

    private static string Row(CriterionPairing p) => p.By switch
    {
        PairedBy.Name => $"#{p.Position + 1} {Status(p)} (by name)",
        PairedBy.Position =>
            $"#{p.Position + 1} {Status(p)} (by position — the disposition named nothing that matches)",
        _ => $"#{p.Position + 1} unmet (no disposition reached it)",
    };

    private static string Status(CriterionPairing p) => p.Disposition?.Status switch
    {
        AcceptanceStatus.Met => "met",
        AcceptanceStatus.NotApplicable when p.Satisfied => "not applicable",
        AcceptanceStatus.NotApplicable => "not applicable without a reason",
        _ => "unmet",
    };
}
