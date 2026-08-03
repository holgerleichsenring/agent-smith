namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0315e: one drafted phase spec inside an outcome proposal. Yaml is the
/// full schema-valid spec text (what p0315c files into the ticket body);
/// PhaseId / Goal / Requires are extracted for display and requires-edge
/// consistency checks.
/// </summary>
public sealed record PhaseDraft(
    string PhaseId,
    string Goal,
    string Yaml,
    IReadOnlyList<string> Requires)
{
    /// <summary>
    /// p0393a: the phase's done-criteria, read out of the same yaml. They are the
    /// run's ACCEPTANCE CONTRACT once the code pipeline derives a spec for every
    /// ticket — the list the keystone scores, the pull request renders and result.md
    /// reports against, in the slot p0328's negotiated expectation used to fill.
    /// </summary>
    public IReadOnlyList<string> Done { get; init; } = [];
}
