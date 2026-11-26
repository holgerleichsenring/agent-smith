using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-023fa: the merge step of a code run and of a merge-green run; nothing here fails the run, whose work is
/// committed and open either way.
/// <para>2026-11-25-96aea: a code run merges nothing and marks nothing awaiting CI — the series master holding its ticket
/// (a series of one for a standalone ticket) merges it. Only a merge-green run merges: one the series master or a person
/// asked for (<see cref="ContextKeys.CiGreenMerge"/>), in order (<see cref="PullRequestMergeSequence"/>). A request for
/// not-started builds queues them again (<see cref="PullRequestBuildRequeue"/>); one for pull requests without CI starts it
/// (<see cref="PullRequestCiStart"/>); neither merges.</para>
/// </summary>
public sealed class CompletePullRequestsHandler(
    PullRequestMergeSequence merges, PullRequestCiStart? ciStart = null, PullRequestBuildRequeue? requeue = null)
    : ICommandHandler<CompletePullRequestsContext>
{
    public const string LeftToTheMaster = "Not merged: the series master holding this ticket merges its pull requests.";

    public async Task<CommandResult> ExecuteAsync(CompletePullRequestsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Pipeline.TryGet<CiGreenMergeRequest>(ContextKeys.CiGreenMerge, out var green) && green is not null
            ? await GreenAsync(context, green, cancellationToken)
            : CommandResult.Ok(LeftToTheMaster);
    }

    private async Task<CommandResult> GreenAsync(CompletePullRequestsContext context, CiGreenMergeRequest request, CancellationToken ct)
    {
        if (request.PullRequests.Any(p => p.NotStarted)) return await RequeueAsync(context, request, ct);
        if (request.PullRequests.Any(p => p.NoCi))
            return CommandResult.Ok($"CI for the pull requests — {(ciStart is null ? "none started" : await ciStart.StartAsync(context, request, ct))}; nothing merges.");
        if (request.AskedBy is null) return CommandResult.Ok("Not merged: nobody asked for this merge.");
        var entries = request.PullRequests.Select(p => new OpenedPullRequest(p.Repo, p.Url, OpenStatus.Opened)).ToList();
        var lines = await merges.RunAsync(context, entries, new BranchName(request.Branch), request.AttemptRunId, ct, PullRequestMergeAsk.Of(request));
        if (context.Pipeline.TryGet<ActSlot<IReadOnlyList<PullRequestMergeLine>>>(ContextKeys.ActOutcome, out var slot)) slot?.Set(lines);
        return CommandResult.Ok($"asked by {request.AskedBy} ({Mode(context.Protection)}) — {string.Join("; ", lines.Select(l => l.Text))}");
    }

    // 2026-11-24-629aa: each not-started pull request's required builds queued again; a repo not in the run queues nothing.
    private async Task<CommandResult> RequeueAsync(CompletePullRequestsContext context, CiGreenMergeRequest request, CancellationToken ct)
    {
        var said = new List<string>();
        foreach (var pr in request.PullRequests.Where(p => p.NotStarted)) said.Add($"{pr.Repo}: {await QueuedAsync(context, pr, ct)} queued");
        return CommandResult.Ok($"required builds that had not started, queued again — {string.Join("; ", said)}; nothing merges.");
    }

    private async Task<int> QueuedAsync(CompletePullRequestsContext context, CiGreenPullRequest pr, CancellationToken ct) =>
        requeue is not null && context.Configs.FirstOrDefault(r => r.Name == pr.Repo) is { } repo
            ? await requeue.RequeueAsync(repo, pr.Url, ct, headSettled: pr.NotStarted) : 0;

    private static string Mode(BranchProtection protection) =>
        protection == BranchProtection.Respect ? "under the host's branch protection" : "past branch protection";
}
