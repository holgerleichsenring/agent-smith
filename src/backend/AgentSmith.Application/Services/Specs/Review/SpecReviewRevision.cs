using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: the ONE publisher of a revision made during a run — a review round's cuts, a rewrite, a
/// series round, a hand-back, re-grounded citations. Each replaced spec keeps the repositories it was placed
/// in (a yaml naming none was placed in memory only), the set keeps its approval, one revision naming the
/// cause is appended, and the set goes through <see cref="ISpecSetPublisher"/> under the spec record's scope.
/// A revision the branch did not take FAILS: building from a reviewed text nobody can read back would build
/// a contract the branch does not state.
/// </summary>
public sealed class SpecReviewRevision(ISpecSetPublisher publisher)
{
    public async Task<CommandResult> PublishAsync(
        PipelineContext pipeline, IReadOnlyList<SpecDraft> replacements, string cause, CancellationToken ct,
        SpecHandback? handback = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(replacements);
        var set = pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        var revised = set with
        {
            Specs = [.. set.Specs.Select(spec => Replace(spec, replacements))],
            Revisions = [.. set.Revisions, new SpecRevision(set.Revisions.Count + 1, cause, DateTimeOffset.UtcNow)],
            Handback = handback ?? set.Handback,
        };
        var before = Sha(pipeline);
        var result = await publisher.PublishAsync(pipeline, Project(pipeline), Scope(pipeline), revised, [], ct);
        if (!result.IsSuccess || pipeline.Flag(ContextKeys.SpecDerivationPreview)) return result; // bdd0d: a preview writes no branch
        return Sha(pipeline) is { } after && after != before
            ? result
            : CommandResult.Fail($"Spec {set.Key} revision {set.Revisions.Count + 1} ({cause}) was not written to "
                + $"the ticket branch: {result.Message}");
    }

    private static DerivedSpec Replace(DerivedSpec spec, IReadOnlyList<SpecDraft> replacements) =>
        replacements.FirstOrDefault(d => d.SpecId == spec.SpecId) is { } draft
            ? spec with { Draft = draft with { Repos = spec.Draft.Repos } }
            : spec;

    // The spec record's own scope: the carrying repository published by the set's first write.
    private static SeriesScope Scope(PipelineContext pipeline)
    {
        var carrying = pipeline.TryGet<string>(ContextKeys.SpecRepo, out var name) ? name : null;
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r) && r is not null ? r : [];
        return new SeriesScope(repos, SeriesScope.LegacyOf(null, carrying, repos), [], carrying);
    }

    private static string Project(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.ProjectName, out var name) && !string.IsNullOrWhiteSpace(name) ? name! : string.Empty;

    private static string? Sha(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.SpecRevisionSha, out var sha) ? sha : null;
}
