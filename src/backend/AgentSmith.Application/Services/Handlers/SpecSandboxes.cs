using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-17-a196a: the sandboxes a spec's own checks run over — those of the repositories its
/// <c>repos:</c> names. A spec naming none (written before specs named them) and a run with no current
/// spec keep every sandbox. A spec naming repositories that no sandbox of the run serves is
/// UNRESOLVED, never widened to the rest: checking another repository instead is the defect this closes.
/// </summary>
public sealed class SpecSandboxes(SandboxTargets targets)
{
    /// <summary>False when the run holds no sandboxes. <paramref name="unresolved"/> names a spec whose
    /// repositories no sandbox serves; the dictionary is then empty.</summary>
    public bool TryResolve(
        PipelineContext pipeline,
        out IReadOnlyDictionary<string, ISandbox> sandboxes,
        out string? unresolved)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        unresolved = null;
        if (!targets.TryResolve(pipeline, out sandboxes, out _)) return false;
        if (!pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var spec) || spec is not { Repos.Count: > 0 })
            return true;

        var owned = spec.Repos
            .SelectMany(name => targets.SandboxesForRepo(pipeline, RepoNamed(pipeline, name)))
            .DistinctBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        if (owned.Count == 0)
            unresolved = $"Spec {spec.SpecId} names {string.Join(", ", spec.Repos)}, and no sandbox of this run "
                         + "serves it; its checks cannot run over another repository instead.";
        sandboxes = owned;
        return true;
    }

    private static RepoConnection RepoNamed(PipelineContext pipeline, string name) =>
        pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is not null
            ? repos.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
              ?? new RepoConnection { Name = name }
            : new RepoConnection { Name = name };
}
