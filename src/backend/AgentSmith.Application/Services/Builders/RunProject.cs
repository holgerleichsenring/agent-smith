using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// 2026-11-17-e061a: the run's project with the run's own repositories — the ones the prologue resolved and
/// CheckoutSource clones (overrides applied) — never the configuration's entry read a second time. Extracted from
/// the 'file' run's builder (5701b) for the runs that act on a filed ticket.
/// </summary>
public static class RunProject
{
    public static ResolvedProject Of(ResolvedProject project, PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is { Count: > 0 }
            ? project with { Repos = repos }
            : project;
}
