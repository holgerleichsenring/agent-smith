namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Narrow projection of ProjectMap that is fed to the triage prompt.
/// Stack/Type/Concepts are the prefiltered concept list — concept matching against
/// the ConceptVocabulary happens in ProjectMapExcerptBuilder, not in the LLM.
/// </summary>
public sealed record ProjectMapExcerpt(
    IReadOnlyList<string> Stack,
    string Type,
    IReadOnlyList<string> Concepts,
    TestCapability TestCapability,
    CiCapability CiCapability);
