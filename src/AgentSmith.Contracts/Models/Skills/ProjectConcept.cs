namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Single entry in the controlled concept vocabulary referenced by skill activation
/// positive keys. Section is one of project_concepts, change_signals, run_context.
/// </summary>
public sealed record ProjectConcept(string Key, string Desc, string Section);
