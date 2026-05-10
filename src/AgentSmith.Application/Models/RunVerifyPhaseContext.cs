using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the RunVerifyPhase step (p0129a). The handler reads PlanJson + DiffJson
/// from <see cref="PipelineContext"/>, dispatches active VerifyDiff investigator skills
/// (those with <c>investigator_mode=verify_diff</c> whose <c>activates_when</c> matches),
/// and decides whether to re-loop (first-fail) or escalate (second-fail).
/// </summary>
public sealed record RunVerifyPhaseContext(
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
