using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-10-864f: the uploaded material a review may read, keyed by its address <c>reference:&lt;name&gt;</c> — the
/// address the design partner reads and cites it under. A design turn holds each upload as its own sandbox; a run
/// carries each into its carrying repository. Either way a reviewer and the evidence check read it through a
/// <see cref="ReviewReferenceView"/>, which serves reads only. Empty when the turn or run holds none.
/// </summary>
public static class ReviewReferences
{
    public static IReadOnlyDictionary<string, ISandbox> For(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var views = new Dictionary<string, ISandbox>(StringComparer.Ordinal);
        if (pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map) && map is not null)
            foreach (var (key, sandbox) in map)
                if (key.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal) && sandbox is ISourceScopeSandbox upload)
                    views[key] = ReviewReferenceView.Over(key, upload);
        if (pipeline.TryGet<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets, out var carried) && carried is not null
            && SpecReviewRepositories.OnePerRepository(pipeline) is { } repositories)
            foreach (var set in carried)
                if (repositories.GetValueOrDefault(set.Repo) is { } repository)
                    views.TryAdd(set.Address, ReviewReferenceView.Carried(set.Address, repository, set.Path));
        return views;
    }

    /// <summary>The uploads beside <paramref name="repositories"/>, for a review look; null when there are neither.</summary>
    public static IReadOnlyDictionary<string, ISandbox>? Beside(
        PipelineContext pipeline, IReadOnlyDictionary<string, ISandbox>? repositories)
    {
        var references = For(pipeline);
        if (references.Count == 0) return repositories;
        var readable = new Dictionary<string, ISandbox>(StringComparer.Ordinal);
        foreach (var (key, sandbox) in repositories ?? new Dictionary<string, ISandbox>()) readable[key] = sandbox;
        foreach (var (address, view) in references) readable[address] = view;
        return readable;
    }
}
