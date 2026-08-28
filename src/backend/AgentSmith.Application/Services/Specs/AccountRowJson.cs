using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-9749: the account's answer exactly as it arrives on the wire, before it is a
/// typed row.
/// <para>
/// The disposition gained a third value and the wire had to keep carrying the second: the
/// scripted harness clients, and any model that answers the older shape, still write
/// <c>"satisfied": true|false</c>. A record whose constructor takes an enum cannot read
/// both spellings, so the reading happens here and <see cref="AccountRow"/> stays a typed
/// value with one disposition.
/// </para>
/// </summary>
internal sealed record AccountRowJson
{
    public string? Criterion { get; init; }

    /// <summary>The three-state answer, spelled as the run-story acceptance vocabulary
    /// spells it: <c>satisfied</c>, <c>not_applicable</c>, <c>not_satisfied</c>.</summary>
    public string? Disposition { get; init; }

    /// <summary>The older two-state answer. Read only when no disposition was written, so a
    /// model that answers both cannot have the weaker field win.</summary>
    public bool? Satisfied { get; init; }

    public string? Citation { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<string>? Citations { get; init; }

    public string? Antecedent { get; init; }

    public AccountRow ToRow() =>
        new(Criterion ?? string.Empty, DispositionOf(), Citation, Note, Citations, Antecedent);

    /// <summary>
    /// Anything unrecognised is NOT SATISFIED. A misspelled disposition is the account
    /// failing to answer, and the floor is the only safe reading of an answer nobody can
    /// interpret.
    /// </summary>
    private AccountDisposition DispositionOf() => Disposition?.Trim().ToLowerInvariant() switch
    {
        "satisfied" or "met" or "yes" or "true" => AccountDisposition.Satisfied,
        "not_applicable" or "not-applicable" or "n/a" or "na" => AccountDisposition.NotApplicable,
        null or "" => Satisfied is true ? AccountDisposition.Satisfied : AccountDisposition.NotSatisfied,
        _ => AccountDisposition.NotSatisfied,
    };
}
