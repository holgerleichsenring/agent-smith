using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-50c7: builds the scope a bootstrap round's baseline is taken for. The other
/// components of the repository are this run's discovered ones, each claiming its workdir and the
/// sources its earlier round recorded; the round's own sources are read from the context.yaml it
/// just wrote, and the workdir it owned before from the context.yaml it started from.
/// </summary>
public sealed class StructureBaselineScopes(StructureContextSources contextSources, StructureOwnership ownership)
{
    public async Task<StructureBaselineScope> ForAsync(PipelineContext pipeline, ISandbox sandbox,
        BootstrapRoundContext round, PrinciplesMode mode, string? contextYamlBefore, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(round);
        var sources = await contextSources.WrittenAsync(sandbox, round.ContextName, ct);
        return new StructureBaselineScope(round.ContextName, round.Workdir, Siblings(pipeline, round), mode)
        {
            Sources = sources,
            WorkdirBefore = contextSources.Workdir(contextYamlBefore),
        };
    }

    private List<StructureClaim> Siblings(PipelineContext pipeline, BootstrapRoundContext round)
    {
        pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents, out var byRepo);
        pipeline.TryGet<List<BootstrapRoundOutcome>>(ContextKeys.BootstrapOutcomes, out var earlier);
        var recorded = (earlier ?? []).Where(o => o.RepoName == round.RepoName)
            .GroupBy(o => o.ContextName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Sources, StringComparer.Ordinal);
        return [.. (byRepo?.GetValueOrDefault(round.RepoName) ?? [])
            .Where(c => c.Name != round.ContextName)
            .SelectMany(c => ownership.Claims(c.Name, c.Workdir, recorded.GetValueOrDefault(c.Name)))];
    }
}
