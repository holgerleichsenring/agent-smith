namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0413: the classifier's shape verdict — the shape plus the ONE line that says
/// what makes the work that shape. The reason travels with the shape everywhere
/// (run row, run view, derivation prompt) so an operator can see why a ticket got
/// the process it got instead of being handed a bare label.
/// </summary>
/// <param name="Shape">The stated shape; <see cref="WorkShape.Unknown"/> when the
/// model omitted or malformed it, which changes nothing downstream.</param>
/// <param name="Reason">One line, verbatim from the classifier. Null when absent.</param>
public sealed record WorkShapeVerdict(WorkShape Shape, string? Reason = null)
{
    /// <summary>The wire spelling of the shape ("deterministic" / "judgement" /
    /// "mixed" / "unknown") — the same lowercase convention the complexity tier
    /// uses on the run row and the dashboard.</summary>
    public string Name => Shape.ToString().ToLowerInvariant();

    /// <summary>The one-line sentence the run view and the derivation prompt show:
    /// the shape, and the reason when the classifier gave one.</summary>
    public override string ToString() =>
        string.IsNullOrWhiteSpace(Reason) ? Name : $"{Name} — {Reason}";
}
