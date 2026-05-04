namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Input to the triage LLM call. Built from ticket + project map + skill index +
/// the preset's declared phases + ticket labels (used to apply hard overrides).
/// </summary>
public sealed record TriageInput(
    string Ticket,
    ProjectMapExcerpt ProjectMapExcerpt,
    IReadOnlyList<SkillIndexEntry> AvailableSkills,
    IReadOnlyList<PipelinePhase> Phases,
    IReadOnlyList<string> TicketLabels);
