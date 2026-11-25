using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: one series run as its page reads it — tickets in depth order with their pull requests and
/// CI per repo, progress and merge order — from the same set-based reads as the list; null when there is no such series
/// or its project is no longer configured. 2026-11-23-31dcd: and the bar's facts — the repos it touches, what it cost,
/// and its conversation's thread, so the page can carry that conversation. 2026-11-23-31dcf: and the requires edges
/// between its tickets (<see cref="SeriesRunEdges"/>; 2026-11-23-f562: without kinds). One read after another on the request's
/// unit of work. 2026-11-23-31dcg: and each ticket's hold, as the ticket list reads it (<see cref="SeriesRunHolds"/>).
/// 2026-11-23-0219: and whether a merge arms auto-complete, and per waiting pull request the newest merge run's refusal or arm.</summary>
public sealed class SeriesRunDetailReader(
    ISeriesRunStore store, TicketProjects projects, SeriesStatusReader status, SeriesRunNames names,
    SpecDialogSessionRepository sessions, PullRequestMergeSaidReader mergeSaid)
{
    /// <param name="withHolds">2026-11-23-31dcg: false for a reader that needs the merge order only (the merge act), which
    /// then pays no ticket-list read.</param>
    public async Task<SeriesRunDetailView?> ReadAsync(string id, CancellationToken ct, bool withHolds = true)
    {
        if (await store.GetAsync(id, ct) is not { } record || projects.Of(record.Project) is not { } project) return null;
        // 2026-11-25-9992a: the one state; 2026-11-23-8d16: a project with no held ticket reads as no holds, not as holds unread.
        var read = (await status.StatusesAsync([record], withHolds, ct))[record.Id];
        var (facts, shape, held) = (read.ProjectFacts, read.Shape, read.Holds);
        var shown = withHolds ? await WithMergeSaidAsync(shape.Tickets, facts, ct) : shape.Tickets;
        var name = (await names.OfAsync([record], ct))[record.Id];
        var spent = read.Facts.Sum(f => f.CostUsd);
        var session = await sessions.GetBySessionIdAsync(record.Conversation, ct);
        return new SeriesRunDetailView(record.Id, name, record.Project, record.State, record.Conversation, record.StartedAt,
            record.FinishedAt, shape.Counts, SeriesRunQuestion.Of(record, held), shown, shape.MergeOrder)
        {
            WorkingOn = SeriesWorkingOn.Of(read.Facts), // 2026-11-25-9992a
            // 2026-11-24-f9c5c: a merge into a series branch never arms auto-complete; it merges once green.
            Repos = Repos(project, shape.Tickets), MergeArms = record.IntegrationBranch is null && project.Merge.RespectBranchProtection,
            IntegrationBranch = record.IntegrationBranch, CostUsd = spent + record.MasterCostUsd,
            Deliveries = [.. record.Deliveries.Where(d => d.Url.Length > 0).Select(d => new SeriesRunDeliveryView(d.Repo, d.Url, d.Number, d.Said))],
            MergeWhen = project.Merge.When switch // 2026-11-24-f9c5f: the YAML's names
            {
                AgentSmith.Contracts.Models.Configuration.MergeWhen.OnGateGreen => "on_gate_green",
                AgentSmith.Contracts.Models.Configuration.MergeWhen.OnCiGreen => "on_ci_green",
                _ => "manual",
            },
            ThreadId = session?.ThreadId, ConversationOpen = session?.IsOpen == true,
            Edges = SeriesRunEdges.Of(facts, [.. record.Tickets.Select(t => t.TicketId)]),
        };
    }

    // 2026-11-23-0219: each waiting pull request with what the newest merge run said about it when it did not merge it — unless
    // its head moved since, which makes the word one about an older change.
    private async Task<IReadOnlyList<SeriesRunTicketView>> WithMergeSaidAsync(
        IReadOnlyList<SeriesRunTicketView> shown, SeriesRunProjectFacts facts, CancellationToken ct)
    {
        IReadOnlyCollection<string> waiting =
            [.. shown.SelectMany(t => t.PullRequests).Where(SeriesRunPullRequestView.Waits).Select(p => p.Url).Distinct(StringComparer.Ordinal)];
        var said = await mergeSaid.NewestAsync(waiting, ct);
        var refreshing = (await mergeSaid.RefreshesAsync(waiting, ct)).Where(r => r.Value.InFlight) // 2026-11-24-6811d
            .ToDictionary(r => r.Key, r => r.Value.RunId, StringComparer.Ordinal);
        if (refreshing.Count > 0)
            shown = [.. shown.Select(t => t with { PullRequests = [.. t.PullRequests.Select(p =>
                refreshing.TryGetValue(p.Url, out var run) ? p with { RefreshRunId = run } : p)] })];
        if (said.Count == 0) return shown;
        var moved = facts.PullRequests.SelectMany(g => g).Where(r => r.HeadMovedAt is not null)
            .GroupBy(r => r.Url, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Max(r => r.HeadMovedAt!.Value), StringComparer.Ordinal);
        bool Current(string url, PullRequestMergeSaid s) => !moved.TryGetValue(url, out var at) || s.At >= at;
        return [.. shown.Select(t => t with { PullRequests = [.. t.PullRequests.Select(p =>
            SeriesRunPullRequestView.Waits(p) && said.TryGetValue(p.Url, out var s) && Current(p.Url, s) ? p with { MergeSaid = s } : p)] })];
    }

    // The repos the series' tickets have pull requests in, in the project's repo order.
    private static IReadOnlyList<string> Repos(ResolvedProject project, IReadOnlyList<SeriesRunTicketView> tickets)
    {
        var touched = tickets.SelectMany(t => t.Repos).ToHashSet(StringComparer.Ordinal);
        var configured = project.Repos.Select(r => r.Name).ToList();
        return [.. configured.Where(touched.Contains), .. touched.Except(configured).Order(StringComparer.Ordinal)];
    }
}
