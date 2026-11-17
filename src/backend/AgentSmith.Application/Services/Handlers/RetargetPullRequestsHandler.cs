using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-9a39c: the step of a 'retarget' run — moves each of a stacked dependent's pull requests that is still
/// based on a landed prerequisite's branch onto the base the server chose, and puts the count moved into the outcome
/// slot. A pull request someone moved meanwhile is left; one the provider refuses to move (closed, a deleted base) is
/// named in the result. Only missing seeds fail the step.
/// </summary>
public sealed class RetargetPullRequestsHandler(ISourceProviderFactory sources) : ICommandHandler<RetargetPullRequestsContext>
{
    public async Task<CommandResult> ExecuteAsync(RetargetPullRequestsContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<RetargetPullRequestsRequest>(ContextKeys.RetargetPullRequestsRequest, out var request) || request is null
            || !pipeline.TryGet<ActSlot<int>>(ContextKeys.ActOutcome, out var slot) || slot is null)
            return CommandResult.Fail("A 'retarget' run needs the request and outcome slot the server seeds under "
                + $"ContextKeys.{nameof(ContextKeys.RetargetPullRequestsRequest)} and ContextKeys.{nameof(ContextKeys.ActOutcome)}. Nothing was moved.");
        var lines = new List<string>();
        var moved = 0;
        foreach (var move in request.Moves)
        {
            var line = await MoveAsync(context, move);
            if (line is null) moved++;
            else lines.Add(line);
        }
        slot.Set(moved);
        return CommandResult.Ok($"Ticket {request.TicketId}: moved {moved} of {request.Moves.Count} pull request(s)"
            + (lines.Count > 0 ? "; " + string.Join("; ", lines) : string.Empty));
    }

    // Null when moved; otherwise why not, in one clause.
    private async Task<string?> MoveAsync(RetargetPullRequestsContext context, PullRequestMove move)
    {
        if (context.Project.Repos.FirstOrDefault(r => r.Name == move.Repo) is not { } repo)
            return $"{move.Url}: repository '{move.Repo}' is not in project '{context.Project.Name}'";
        var provider = sources.Create(repo);
        var current = await provider.ReadPullRequestBaseAsync(move.Url, CancellationToken.None);
        if (!string.Equals(current, move.From, StringComparison.Ordinal))
            return $"{move.Url} is based on '{current ?? "an unreadable base"}', not '{move.From}'; left";
        return await provider.RetargetPullRequestAsync(move.Url, new BranchName(move.To), CancellationToken.None)
            ? null
            : $"{move.Url}: the provider refused the move from '{move.From}' to '{move.To}' (closed, or the base is gone)";
    }
}
