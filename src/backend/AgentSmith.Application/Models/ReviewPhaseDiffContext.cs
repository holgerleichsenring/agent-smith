using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-17-042eh: context for the ReviewPhaseDiff step — the agent config the review call
/// is made under, and the pipeline bag the phase, its start heads, its diff and its principles
/// are all read from.
/// </summary>
public sealed record ReviewPhaseDiffContext(
    AgentConfig AgentConfig, PipelineContext Pipeline) : ICommandContext;
