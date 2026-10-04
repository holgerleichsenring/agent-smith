using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Extensions;

/// <summary>
/// Extension methods for PipelineContext to avoid duplication across handlers.
/// </summary>
public static class PipelineContextExtensions
{
    public static void AppendDecisions(this PipelineContext pipeline, IReadOnlyList<PlanDecision> decisions)
    {
        pipeline.TryGet<List<PlanDecision>>(ContextKeys.Decisions, out var existing);
        var all = existing ?? new List<PlanDecision>();
        all.AddRange(decisions);
        pipeline.Set(ContextKeys.Decisions, all);
    }

    /// <summary>
    /// 2026-09-15-d66f: which artefact paths THIS RUN already wrote in the named repository.
    /// A repository fans out one round per component against a single checkout, so a
    /// repository-root artefact written by the first component is present for the second —
    /// and reporting that as "preserved as ratified" credits the operator with this run's
    /// own write.
    /// </summary>
    public static IReadOnlySet<string> ArtefactsWrittenIn(this PipelineContext pipeline, string repoName)
    {
        pipeline.TryGet<Dictionary<string, HashSet<string>>>(ContextKeys.ArtefactsWritten, out var byRepo);
        return byRepo is not null && byRepo.TryGetValue(repoName, out var paths)
            ? paths
            : new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>Records what this round wrote, so later rounds of the same run can tell.</summary>
    public static void RememberArtefactWrites(
        this PipelineContext pipeline, string repoName, IReadOnlyList<ArtefactWrite> writes)
    {
        var written = writes.Where(w => w.Status == ArtefactStatus.Written).ToList();
        if (written.Count == 0) return;
        pipeline.TryGet<Dictionary<string, HashSet<string>>>(ContextKeys.ArtefactsWritten, out var byRepo);
        byRepo ??= new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        if (!byRepo.TryGetValue(repoName, out var paths))
            byRepo[repoName] = paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var write in written) paths.Add(write.Path);
        pipeline.Set(ContextKeys.ArtefactsWritten, byRepo);
    }

    /// <summary>
    /// 2026-09-15-c6e9: rounds ACCUMULATE their outcomes, they do not overwrite. Same idiom as
    /// <see cref="AppendDecisions"/>, and for the same reason: one repository produces one round
    /// per component.
    /// </summary>
    public static void AppendBootstrapOutcome(
        this PipelineContext pipeline, BootstrapRoundOutcome outcome)
    {
        pipeline.TryGet<List<BootstrapRoundOutcome>>(ContextKeys.BootstrapOutcomes, out var all);
        all ??= [];
        all.Add(outcome);
        pipeline.Set(ContextKeys.BootstrapOutcomes, all);
    }

    /// <summary>
    /// The language whose delta a component's principles compose: the per-component language
    /// from discovery wins; the repository-level ProjectMap primary language is the fallback
    /// for pre-discovery fixtures.
    /// </summary>
    public static string ComponentLanguage(
        this PipelineContext pipeline, string repoName, string contextName, ProjectMap projectMap)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
                ContextKeys.DiscoveredComponents, out var perRepo) && perRepo is not null
            && perRepo.TryGetValue(repoName, out var components) && components is not null)
        {
            var component = components.FirstOrDefault(
                c => string.Equals(c.Name, contextName, StringComparison.OrdinalIgnoreCase));
            if (component is not null && !string.IsNullOrWhiteSpace(component.Language))
                return component.Language;
        }
        return projectMap.PrimaryLanguage ?? string.Empty;
    }
}
