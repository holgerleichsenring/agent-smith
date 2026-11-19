using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0331: understand the ticket, THEN provision. Runs after FetchTicket and before CheckoutSource (the first
/// sandbox-requiring step). It publishes the repositories an approval names, else the project's
/// (<see cref="ApprovedRepoScope"/>), builds the pre-checkout context inventory, and makes one cheap call.
/// <para>
/// The call estimates the ticket's SIZE (the cost ceiling) and SHAPE (the run row) on every ticketed run, and is
/// the run's only pre-sandbox SECURITY judgement: a ticket that demands what must not be done is refused here and
/// ScopeRefusalRecorder ends the run at the hand-back step — no sandbox, no staged credential.
/// </para>
/// <para>2026-11-18-9c1cb: it narrows nothing; 2026-11-19-6b0bb: it no longer asks which repositories or contexts
/// the work touches — the coding master finds that out (ensure_repo_sandbox).</para>
/// </summary>
public sealed class ScopeReposHandler(
    ApprovedRepoScope approvedScope,
    RemoteContextInventoryBuilder inventoryBuilder,
    RepoScopeClassifier classifier,
    ScopeEstimateRecorder estimates,
    ScopeRefusalRecorder refusals)
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
        // p0341c/p0413: the same call estimates size and shape.
        await estimates.ApplyAsync(pipeline, reply.Estimate, cancellationToken);
        // 2026-10-12-6bfae: a failed call is said, never filed under the silent "skipped".
        return CommandResult.Ok(reply.CallFailed
            ? $"No ticket estimate: the scope call failed — {reply.Error}"
            : $"Repo scoping skipped: the run keeps its {repos.Count} repository/repositories; the ticket is estimated");
    }
}
