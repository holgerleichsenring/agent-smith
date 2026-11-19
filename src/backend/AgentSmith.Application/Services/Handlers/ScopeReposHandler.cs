using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0331: understand the ticket, THEN provision. Runs after FetchTicket and
/// before CheckoutSource (the first sandbox-requiring step). Two jobs:
/// 1. Build the pre-checkout remote context inventory (RemoteContextInventoryBuilder).
/// 2. One cheap LLM call estimates the ticket and may refuse it.
/// 2026-11-18-9c1cb: it no longer narrows ContextKeys.Repos or the contexts — the run keeps the
/// repositories an approval or the project names, and the master scopes itself.
/// <para>
/// p0413: the same reply also estimates the ticket's SIZE and its SHAPE;
/// ScopeEstimateRecorder turns both into run state (cost cap / derivation input).
/// p0413a: the estimate is asked for on EVERY ticketed run, in the same one call.
/// Only SCOPING is meaningless when there is one repository — how much work the
/// ticket is and what kind of work it is are questions a single-repo run has too,
/// and skipping them left it on the static ceiling, cut blind. A single-repo run
/// therefore estimates like any other.
/// </para>
/// <para>
/// The same reply carries the run's only pre-sandbox SECURITY judgement: a ticket
/// that demands what must not be done is refused here, before the estimate and the
/// single-repo return, and ScopeRefusalRecorder ends the run at the hand-back step —
/// no sandbox, no staged credential, no model call with tools.
/// </para>
/// <para>
/// 2026-09-08-1830: the contexts the reply NAMES are recorded on every ticketed run,
/// single-repo included, as the claim the derivation's cut is held against. Naming is
/// not narrowing.
/// </para>
/// <para>
/// 2026-09-17-0e79a: ApprovedRepoScope answers WHICH repositories first — an approval names them
/// and they are published before the inventory.
/// </para>
/// </summary>
public sealed class ScopeReposHandler(
    ApprovedRepoScope approvedScope,
    RemoteContextInventoryBuilder inventoryBuilder,
    RepoScopeClassifier classifier,
    ScopeEstimateRecorder estimates,
    ScopeRefusalRecorder refusals,
    ScopeNamedContextsRecorder namedContexts)
    : ICommandHandler<ScopeReposContext>
{
    public async Task<CommandResult> ExecuteAsync(
        ScopeReposContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var scope = await approvedScope.OpenAsync(pipeline, context.Ticket, cancellationToken);
        if (scope.Error is not null) return CommandResult.Fail(scope.Error);
        var repos = scope.Repos;
        var inventory = await inventoryBuilder.BuildAsync(pipeline, repos, cancellationToken);

        if (context.Ticket is null) return CommandResult.Ok("Repo scoping skipped: run has no ticket");

        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(
            ContextKeys.TicketComments, out var c) ? c : null;
        var reply = await classifier.ClassifyAsync(
            context.Ticket, comments, repos, inventory, context.AgentConfig, pipeline, cancellationToken);
        if (reply.Refusal is not null) return refusals.Apply(pipeline, reply.Refusal);
        // p0341c/p0413: the SAME call estimates size and shape, independent of the scope's confidence.
        await estimates.ApplyAsync(pipeline, reply.Estimate, cancellationToken);
        // p0413a: --repo (or a one-repo project) already decided WHICH repositories; the estimate is
        // recorded. 2026-10-12-6bfae: a failed call is said, never filed under the silent "skipped".
        // 2026-11-18-9c1cb: the run keeps the repositories the approval or the project names; the master
        // scopes itself (ensure_repo_sandbox). The reply's contexts are recorded, never used to narrow.
        namedContexts.Record(pipeline, reply.Classification, repos, inventory);
        return CommandResult.Ok(reply.CallFailed
            ? $"No ticket estimate: the scope call failed — {reply.Error}"
            : $"Repo scoping skipped: the run keeps its {repos.Count} repository/repositories; the ticket is estimated");
    }
}
