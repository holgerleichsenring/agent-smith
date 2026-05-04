namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Output shape constraints for a skill. Each declared role has its own OutputForm
/// (lead → Plan, analyst/reviewer → List, filter → List or Artifact).
/// </summary>
public sealed record OutputContract(
    string SchemaRef,
    int MaxObservations,
    int MaxCharsPerField,
    IReadOnlyDictionary<SkillRole, OutputForm> OutputType);
