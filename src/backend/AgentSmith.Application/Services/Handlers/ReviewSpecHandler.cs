using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Challenges the derived contract against the repository before a single master token is
/// spent on it.
/// <para>
/// It runs between DeriveSpec and SpecHandback on purpose. Before the hand-back, because a
/// criterion no work can satisfy is exactly the case that step exists to park; after the
/// derivation, because the contract has to exist to be challenged — and because the
/// derivation has already published it, a correction is a new revision of the artifact on
/// the branch, never an edit in memory that would leave the branch stating a contract
/// nobody is judged by.
/// </para>
/// </summary>
public sealed class ReviewSpecHandler(
    ISpecSetPublisher publisher,
    SpecReviewPass pass,
    LoopLimitsConfig limits,
    ILogger<ReviewSpecHandler> logger)
    : ICommandHandler<ReviewSpecContext>
{
    public async Task<CommandResult> ExecuteAsync(
        ReviewSpecContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (limits.MaxSpecReviewRounds <= 0)
            return CommandResult.Ok("Spec review is off (limits.max_spec_review_rounds = 0)");
        if (context.Ticket is null) return CommandResult.Ok("Spec review skipped: the run has no ticket");
        if (HandedBackAlready(context.Pipeline))
            return CommandResult.Ok("Spec review skipped: the derivation already handed the ticket back");

        // The derivation's publisher put the set on the run context one step ago; reading
        // it there rather than back off the branch keeps this step working wherever the
        // derivation ran, and keeps the two from disagreeing about what is being judged.
        if (!context.Pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null)
            return CommandResult.Ok("Spec review skipped: the run derived no spec");
        var repo = CarryingRepo(context);
        if (repo is null)
            return CommandResult.Ok("Spec review skipped: no repo carries the spec");

        var outcome = await pass.RunAsync(
            set, context.AgentConfig, SearchOver(context.Pipeline),
            PipelineCostTracker.GetOrCreate(context.Pipeline), cancellationToken);
        return await ActOnAsync(context, repo, outcome, cancellationToken);
    }

    private async Task<CommandResult> ActOnAsync(
        ReviewSpecContext context, RepoConnection repo, SpecReviewOutcome outcome,
        CancellationToken cancellationToken)
    {
        if (outcome.ChangedTheContract)
            await PublishAsync(context, repo, outcome, cancellationToken);
        if (!outcome.ParksTheRun)
            return CommandResult.Ok(outcome.ChangedTheContract
                ? $"The spec review corrected {outcome.Corrected.Count} criterion(s) before the master ran"
                : "The spec review found nothing the repository refuses");

        var reason = SpecReviewHandbackReason.For(
            outcome.ParkedPhaseId ?? "the phase", outcome.ForTheAuthor);
        context.Pipeline.Set(
            ContextKeys.SpecHandback,
            new SpecHandback(SpecHandbackCase.RequirementsContradictRepository, reason));
        logger.LogWarning(
            "The spec review found {Count} criterion(s) no work can satisfy — handing back", 
            outcome.ForTheAuthor.Count);
        return CommandResult.Ok(
            $"The spec review found {outcome.ForTheAuthor.Count} criterion(s) no work can satisfy");
    }

    private async Task PublishAsync(
        ReviewSpecContext context, RepoConnection repo, SpecReviewOutcome outcome,
        CancellationToken cancellationToken)
    {
        var revised = SpecReviewRevision.Of(outcome.Set, outcome.Corrected, DateTimeOffset.UtcNow);
        await publisher.PublishAsync(
            context.Pipeline, ProjectOf(context.Pipeline), repo, revised, [], cancellationToken);
    }

    /// <summary>The repo the derivation wrote the set into, named by the publisher one step
    /// ago; the first of the resolved scope when it named none.</summary>
    private static RepoConnection? CarryingRepo(ReviewSpecContext context) =>
        context.Pipeline.TryGet<string>(ContextKeys.SpecRepo, out var name)
        && !string.IsNullOrWhiteSpace(name)
            ? context.Repos.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.Ordinal))
              ?? context.Repos.FirstOrDefault()
            : context.Repos.FirstOrDefault();

    /// <summary>The same read-only look the delivery account gets, over the branch as it
    /// stands now. No base ref: there is no delivery yet to compare one against.</summary>
    private BranchSearch? SearchOver(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var boxes)
        && boxes is { Count: > 0 }
            ? new BranchSearch(boxes, logger, searcher: "The spec review")
            : null;

    private static bool HandedBackAlready(PipelineContext pipeline) =>
        pipeline.TryGet<SpecHandback>(ContextKeys.SpecHandback, out var handback)
        && handback is not null && handback.Case != SpecHandbackCase.None;

    private static string ProjectOf(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.ProjectName, out var name)
        && !string.IsNullOrWhiteSpace(name) ? name! : string.Empty;
}
