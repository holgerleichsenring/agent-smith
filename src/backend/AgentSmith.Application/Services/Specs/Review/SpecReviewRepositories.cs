using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: the run's repositories as a spec review reads them. Two toolchain sandboxes of one
/// repository hold the same tree, so a bare citation probed over both is answered by neither; the evidence
/// check therefore takes ONE sandbox per repository name, and stored evidence names the repository, never
/// the sandbox key. Template and reference keys are dropped by <see cref="IsRepositoryKey"/>, the filter
/// <see cref="DerivationLookFactory.ProposalRepositories"/> uses too.
/// </summary>
public static class SpecReviewRepositories
{
    /// <summary>A key that names a repository — not a template, not (283dc) an uploaded reference.</summary>
    public static bool IsRepositoryKey(string key) =>
        !key.StartsWith(TemplateScopeName.Prefix, StringComparison.Ordinal)
        && !key.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal);

    /// <summary>Sandbox key to repository name, as the coordinator published it; empty when it did not.</summary>
    public static IReadOnlyDictionary<string, string> Names(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var names)
            && names is not null ? names : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// The first sandbox of each repository name, keyed by the name. With no published map, the turn's
    /// repositories as the proposal review reads them; null when the run holds none.
    /// </summary>
    public static IReadOnlyDictionary<string, ISandbox>? OnePerRepository(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var names)
            || names is null)
            return DerivationLookFactory.ProposalRepositories(pipeline);
        pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var sandboxes);
        var chosen = new Dictionary<string, ISandbox>(StringComparer.Ordinal);
        foreach (var (key, name) in names)
            if (IsRepositoryKey(key) && sandboxes?.GetValueOrDefault(key) is { } sandbox)
                chosen.TryAdd(name, sandbox);
        return chosen.Count == 0 ? null : chosen;
    }
}
