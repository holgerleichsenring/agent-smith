using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: per-run, master-shared context the sub-agent runner consults when
/// building a child's loop request. The shared sandbox + run cost tracker
/// are deliberately re-used (the children operate against the master's
/// repo working tree and accrue cost against the same per-run total).
/// Identity, tool kit, and policy are per-child resolution paths.
/// </summary>
public sealed record SubAgentContext(
    PipelineContext Pipeline,
    IReadOnlyDictionary<string, ISandbox> Sandboxes,
    PipelineCostTracker CostTracker,
    string MasterRunId,
    IToolKit ToolKit,
    IPipelineToolPolicy ToolPolicy,
    SubAgentBudget Budget,
    string? ParentSubAgentId = null);
