using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-17-042eh: context for the ReviewSpecDiff step — the agent config the review call
/// is made under, and the pipeline bag the spec, its start heads, its diff and its principles
/// are all read from.
/// </summary>
public sealed record ReviewSpecDiffContext(
    AgentConfig AgentConfig, PipelineContext Pipeline) : ICommandContext;
