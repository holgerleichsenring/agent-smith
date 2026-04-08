using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the SpawnFix pipeline step that generates fix requests
/// for Critical/High severity security findings.
/// </summary>
public sealed record SpawnFixContext(
    AutoFixConfig Config,
    PipelineContext Pipeline) : ICommandContext;
