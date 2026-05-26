using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Immutable per-skill output buffer produced by a skill round handler.
/// Carries everything the handler would have written to PipelineContext.
/// Consumed at the executor's merge step in deterministic skill-graph order
/// so parallel rounds never mutate the shared context concurrently.
/// </summary>
public sealed record SkillRoundBuffer(
    string SkillName,
    int Round,
    IReadOnlyList<SkillObservation> Observations,
    DiscussionEntry? DiscussionEntry,
    string? StructuredOutput);
