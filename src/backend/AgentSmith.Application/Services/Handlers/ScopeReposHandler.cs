using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
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
/// 1. Build the pre-checkout remote context inventory — one ResolveAllAsync per
///    repo over ISourceProvider, cached at ContextKeys.RemoteContextInventory so
///    PipelineSandboxCoordinator never re-reads the same context.yamls remotely.
/// 2. One cheap LLM call classifies ticket → affected repos and narrows
///    ContextKeys.Repos to that subset (the ONE seam checkout / sandboxes /
///    CommitAndPR / PrCrossLink all re-read). All-repos fallback on low
///    confidence / parse failure / LLM error / unknown repo name; the decision
///    + rationale is always recorded on the run. A CLI --repo override already
///    narrowed Repos to one entry, so the classifier never overrides the operator.
/// </summary>
public sealed class ScopeReposHandler(
    ISandboxLanguageResolver languageResolver,
    RepoScopeClassifier classifier,
    ILogger<ScopeReposHandler> logger)
    : ICommandHandler<ScopeReposContext>
{
    public async Task<CommandResult> ExecuteAsync(
        ScopeReposContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        var inventory = await BuildInventoryAsync(pipeline, repos, cancellationToken);

        if (repos.Count <= 1)
            return CommandResult.Ok(
                "Repo scoping skipped: single-repo run (one configured repo or --repo override)");
        if (context.Ticket is null)
            return CommandResult.Ok("Repo scoping skipped: run has no ticket");

        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(
            ContextKeys.TicketComments, out var c) ? c : null;
        var (classification, error) = await classifier.ClassifyAsync(
            context.Ticket, comments, repos, inventory, context.AgentConfig, pipeline, cancellationToken);
        var (scoped, record) = RepoScopeEvaluator.Evaluate(classification, error, repos);

        // The scope decision is a run artifact, never silent: a named context key
        // for programmatic consumers + a decision entry result.md / dashboard render.
        pipeline.Set(ContextKeys.RepoScopeRationale, record);
        pipeline.AppendDecisions([new PlanDecision("scope", record)]);
        logger.LogInformation("{Record}", record);

        if (scoped is not null)
            pipeline.Set(ContextKeys.Repos, scoped);
        // p0336b: narrow CONTEXTS within the kept repos (a whole sandbox each),
        // one level below repo-scoping — same conservative keep-all fallback.
        ApplyContextScope(pipeline, classification, error, scoped ?? repos, inventory);
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

    // The inventory covers ALL repos as seen BEFORE narrowing, so a mid-run
    // ensure_repo_sandbox escalation to a descoped repo also hits the cache.
    // p0261 `--context NAME` pins every repo to one named context — the
    // coordinator resolves via ResolveContextAsync then, so no inventory is
    // cached (it would not be consumed).
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>
        BuildInventoryAsync(
            PipelineContext pipeline, IReadOnlyList<RepoConnection> repos, CancellationToken ct)
    {
        var inventory = new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal);
        foreach (var repo in repos)
            inventory[repo.Name ?? string.Empty] = await languageResolver.ResolveAllAsync(repo, ct);

        var contextOverride = pipeline.TryGet<string>(ContextKeys.SourceContext, out var ctx)
            && !string.IsNullOrWhiteSpace(ctx);
        if (!contextOverride)
            pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.RemoteContextInventory, inventory);
        logger.LogInformation(
            "Remote context inventory: {Repos} repo(s), {Contexts} context(s){Cached}",
            inventory.Count, inventory.Values.Sum(v => v.Count),
            contextOverride ? " (not cached — --context override active)" : string.Empty);
        return inventory;
    }
}
