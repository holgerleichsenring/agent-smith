using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for writing run result (plan.md + result.md) to .agentsmith/runs/.
/// </summary>
public sealed record WriteRunResultContext(
    Repository Repository,
    Plan Plan,
    Ticket Ticket,
    IReadOnlyList<CodeChange> Changes,
    PipelineContext Pipeline) : ICommandContext;
