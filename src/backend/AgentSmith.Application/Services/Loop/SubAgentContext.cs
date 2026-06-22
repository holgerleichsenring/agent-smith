using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177/p0280: per-run, master-shared context the sub-agent runner consults when
/// building a child's loop request. The shared sandbox + run cost tracker are
/// deliberately re-used (children operate against the master's repo working tree and
/// accrue cost against the same per-run total). p0280: the master also passes the child
/// tool surface it granted (ChildTools — read-only for a scan master, read/write for a
/// coding master; never spawn_agents) and the answer store children write their final
/// answer to.
/// </summary>
public sealed record SubAgentContext(
    PipelineContext Pipeline,
    IReadOnlyDictionary<string, ISandbox> Sandboxes,
    PipelineCostTracker CostTracker,
    string MasterRunId,
    IReadOnlyList<AITool> ChildTools,
    IChildAnswerStore AnswerStore,
    SubAgentBudget Budget,
    string? ParentSubAgentId = null);
