using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for a security-scan skill round (no ticket, uses code analysis).
/// </summary>
public sealed record SecuritySkillRoundContext(
    string SkillName,
    int Round,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
