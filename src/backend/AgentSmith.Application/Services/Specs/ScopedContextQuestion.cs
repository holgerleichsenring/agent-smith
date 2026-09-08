using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-08-1830: the hand-back a cut becomes when its coverage gap survives the pin.
/// Only the author knows whether the scope call over-read the ticket, so the run asks —
/// as a <see cref="SpecHandbackCase.Question"/>, with the two readings typed and the
/// carrying one taken, so an unanswered re-trigger proceeds on carrying the gap. A
/// hand-back replaces the spec: the phases the cut had are dropped, as the parser drops
/// them for a question the model asks itself.
/// </summary>
public static class ScopedContextQuestion
{
    public static SpecDerivation For(SpecDerivation cut, IReadOnlyList<string> gap, ScopeNamedContexts named)
    {
        ArgumentNullException.ThrowIfNull(cut);
        ArgumentNullException.ThrowIfNull(gap);
        ArgumentNullException.ThrowIfNull(named);
        var missing = string.Join(", ", gap);
        var carried = ScopedContextCoverage.Carried(cut.Set);
        var handback = new SpecHandback(
            SpecHandbackCase.Question,
            Reason(missing, carried, named.Rationale),
            Readings: [$"carry {missing} as well", $"{missing} {Verb(gap)} out of scope for this ticket"],
            Taken: 0);
        var set = cut.Set with
        {
            Phases = [],
            Accounting = SpecAccounting.Empty,
            Handback = handback,
            Executed = [],
        };
        return cut with { Set = set };
    }

    private static string Reason(string missing, IReadOnlyList<string> carried, string? rationale)
    {
        var covered = carried.Count == 0 ? "no context" : string.Join(", ", carried);
        var because = string.IsNullOrWhiteSpace(rationale) ? string.Empty : $" (\"{rationale}\")";
        return $"The scope call named {missing} for this ticket{because}; the cut carries {covered} "
            + $"only and, asked again, still did not say why {missing} is left out. Only the author "
            + "can settle whether the scope call over-read the ticket.";
    }

    private static string Verb(IReadOnlyList<string> gap) => gap.Count == 1 ? "is" : "are";
}
