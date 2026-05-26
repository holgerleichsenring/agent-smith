using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for a single skill round contribution in the plan discussion.
/// </summary>
/// <remarks>
/// p0158g: <see cref="RepoName"/> is the repo this round was dispatched for.
/// Empty when the round is not repo-scoped (legacy single-repo flow or
/// pipelines that don't fan out per repo yet). Forward plumbing — the
/// handler doesn't consume it today; it lets future per-repo SkillRound
/// flows mirror BootstrapRound without another contract change.
///
/// p0161d: <see cref="ContextName"/> + <see cref="Workdir"/> mirror the same
/// per-component scoping shipped on <see cref="BootstrapRoundContext"/>.
/// Forward plumbing only — SkillRoundHandler does not consume them today;
/// the builder propagates whatever the PipelineCommand carried so a future
/// per-context SkillRound flow lands without another contract change.
/// </remarks>
public sealed record SkillRoundContext(
    string SkillName,
    int Round,
    AgentConfig AgentConfig,
    PipelineContext Pipeline,
    string RepoName = "",
    string ContextName = "",
    string Workdir = ".") : ICommandContext;
