namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-28-3793: why a restore was refused, as the sentence the rule wrote. A status
/// code says that something was refused; only this says which rule did it and what the
/// operator would have to change — a differing schema head, or an installation that has
/// already recorded a run.
/// </summary>
/// <param name="Refusal">The rule's own sentence, rendered to the operator verbatim.</param>
public sealed record ArchiveRefusalResponse(string Refusal);
