using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0331: understand the ticket, THEN provision. Runs after FetchTicket and
/// before CheckoutSource (the first sandbox-requiring step). Two jobs:
/// 1. Build the pre-checkout remote context inventory (RemoteContextInventoryBuilder).
/// 2. One cheap LLM call classifies ticket → affected repos and narrows
///    ContextKeys.Repos to that subset (the ONE seam checkout / sandboxes /
///    CommitAndPR / PrCrossLink all re-read). All-repos fallback on low
///    confidence / parse failure / LLM error / unknown repo name; the decision
///    + rationale is always recorded on the run. A CLI --repo override already
///    narrowed Repos to one entry, so the classifier never overrides the operator.
/// <para>
/// p0413: the same reply also estimates the ticket's SIZE and its SHAPE;
/// ScopeEstimateRecorder turns both into run state (cost cap / derivation input).
/// p0413a: the estimate is asked for on EVERY ticketed run, in the same one call.
/// Only SCOPING is meaningless when there is one repository — how much work the
/// ticket is and what kind of work it is are questions a single-repo run has too,
/// and skipping them left it on the static ceiling, cut blind. A single-repo run
/// therefore estimates and stops there: no narrowing, no context scoping, no
/// expected-changes gate — none of those is the estimate.
/// </para>
/// </summary>
public sealed class ScopeReposHandler(
    RemoteContextInventoryBuilder inventoryBuilder,
    RepoScopeClassifier classifier,
    ScopeEstimateRecorder estimates,
    ILogger<ScopeReposHandler> logger)
    : ICommandHandler<ScopeReposContext>
{
    public async Task<CommandResult> ExecuteAsync(
        ScopeReposContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        var inventory = await inventoryBuilder.BuildAsync(pipeline, repos, cancellationToken);

        if (context.Ticket is null)
            return CommandResult.Ok("Repo scoping skipped: run has no ticket");

        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(
            ContextKeys.TicketComments, out var c) ? c : null;
        var reply = await classifier.ClassifyAsync(
            context.Ticket, comments, repos, inventory, context.AgentConfig, pipeline, cancellationToken);
        // p0341c/p0413: the SAME call estimates the ticket's size and its shape. Both are
        // independent of the repo-scope confidence fallback: a low-confidence scope still
        // yields a usable effort estimate and a usable shape.
        await estimates.ApplyAsync(pipeline, reply.Estimate, cancellationToken);
        // p0413a: an operator's --repo override (or a one-repo project) already decided
        // WHICH repositories — the estimate is what was missing, and it is now recorded.
        if (repos.Count <= 1)
            return CommandResult.Ok(
                "Repo scoping skipped: single-repo run (one configured repo or --repo override)");
        var (scoped, record, expectedChanges) =
            RepoScopeEvaluator.Evaluate(reply.Classification, reply.Error, repos);

        // The scope decision is a run artifact, never silent: a named context key
        // for programmatic consumers + a decision entry result.md / dashboard render.
        pipeline.Set(ContextKeys.RepoScopeRationale, record);
        pipeline.AppendDecisions([new PlanDecision("scope", record)]);
        logger.LogInformation("{Record}", record);

        if (scoped is not null)
            pipeline.Set(ContextKeys.Repos, scoped);
        // p0384: the validated must-change subset feeds the keystone's per-repo
        // delivery gate. Published only when present — absent key = anyCode
        // semantics, the classifier imposed no per-repo requirement.
        if (expectedChanges.Count > 0)
            pipeline.Set(ContextKeys.ExpectedChangeRepos, expectedChanges);
        // p0336b: narrow CONTEXTS within the kept repos (a whole sandbox each),
        // one level below repo-scoping — same conservative keep-all fallback.
        ApplyContextScope(pipeline, reply.Classification, reply.Error, scoped ?? repos, inventory);
        return CommandResult.Ok(record);
    }

    private void ApplyContextScope(
        PipelineContext pipeline, RepoScopeClassification? classification, string? error,
        IReadOnlyList<RepoConnection> keptRepos,
        IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> inventory)
    {
        var (contexts, dropped) = ContextScopeEvaluator.Evaluate(classification, error, keptRepos, inventory);
        if (contexts is null || dropped.Count == 0) return;
        pipeline.Set(ContextKeys.ScopedContexts, contexts);
        // The drop is a run artifact, not silent — the coordinator provisions
        // fewer sandboxes and the dashboard shows why (same channel as repo scope).
        var record = "Context scope: dropped " + string.Join(", ", dropped.Select(d => $"{d.Repo}/{d.Context}"));
        pipeline.AppendDecisions([new PlanDecision("scope", record)]);
        logger.LogInformation("{Record}", record);
    }
}
