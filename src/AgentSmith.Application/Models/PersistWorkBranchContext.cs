using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the PersistWorkBranchCommand — runs in the pipeline's failure path
/// when local changes exist that would otherwise be lost with the container's /tmp.
/// </summary>
public sealed record PersistWorkBranchContext(
    SourceConfig Source,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
