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
    IReadOnlyList<string> Requires);
