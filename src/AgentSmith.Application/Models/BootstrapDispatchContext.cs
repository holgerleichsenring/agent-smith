using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the BootstrapDispatch step. The handler reads
/// <c>ContextKeys.AvailableRoles</c>, filters by <c>activates_when</c> against
/// the run-state concepts, and emits exactly one <c>SkillRound</c> command for
/// the matching bootstrap-* skill (or fails loud on 0 / &gt;1 matches).
/// </summary>
public sealed record BootstrapDispatchContext(PipelineContext Pipeline) : ICommandContext;
