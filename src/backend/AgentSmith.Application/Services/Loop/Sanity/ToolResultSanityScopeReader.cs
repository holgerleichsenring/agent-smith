using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// Reads the sanity scope from the pipeline: the current spec's cited paths (its facts' evidence
/// and its steps' targets) and named repositories, and the sandbox-key to repository map. With no
/// current spec the scope is empty, which turns the spec-bound rules off.
/// </summary>
public sealed class ToolResultSanityScopeReader(EvidenceReferences references)
{
    public ToolResultSanityScope Read(PipelineContext pipeline)
    {
        var map = pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var m) && m is not null
            ? m
            : new Dictionary<string, string>();
        if (!pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var spec) || spec is null)
            return new ToolResultSanityScope([], [], map);

        var routing = new ToolResultSanityScope([], spec.Repos, map);
        var fromFacts = spec.Facts
            .SelectMany(f => references.Read(f.Evidence).References)
            .Where(r => !r.IsHostShaped)
            .Select(r => Qualified(routing.Normalise(r.Path), r.Qualifier));
        var fromSteps = spec.Steps
            .Where(s => !string.IsNullOrWhiteSpace(s.Target))
            .Select(s => routing.Normalise(s.Target));
        return routing with { CitedPaths = [.. fromFacts.Concat(fromSteps).Where(p => p.Path != ".").Distinct()] };
    }

    private static ScopedPath Qualified(ScopedPath path, string? qualifier) =>
        path.Repository is null && qualifier is not null ? path with { Repository = qualifier } : path;
}
