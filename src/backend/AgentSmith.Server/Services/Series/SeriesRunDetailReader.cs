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
    SpecDialogSessionRepository sessions, PullRequestMergeSaidReader mergeSaid, TicketAttemptRecords attempts)
{
    /// <param name="withHolds">2026-11-23-31dcg: false for a reader that needs the merge order only (the merge act), which
    /// then pays no ticket-list read.</param>
    public async Task<SeriesRunDetailView?> ReadAsync(string id, CancellationToken ct, bool withHolds = true)
    {
        if (await store.GetAsync(id, ct) is not { } record || projects.Of(record.Project) is not { } project) return null;
        // 2026-11-25-9992a: the one state; 2026-11-23-8d16: a project with no held ticket reads as no holds, not as holds unread.
        var read = (await status.StatusesAsync([record], withHolds, ct))[record.Id];
        var (facts, shape) = (read.ProjectFacts, read.Shape);
        var shown = withHolds ? await WithTrackerAsync(await WithMergeSaidAsync(shape.Tickets, facts, ct), ct) : shape.Tickets;
        var name = (await names.OfAsync([record], ct))[record.Id];
        var spent = read.Facts.Sum(f => f.CostUsd);
        var session = await sessions.GetBySessionIdAsync(record.Conversation, ct);
        var question = SeriesRunQuestion.Waiting(record);
        return new SeriesRunDetailView(record.Id, name, record.Project, record.State, record.Conversation, record.StartedAt,
            record.FinishedAt, shape.Counts, question?.Text, shown, shape.MergeOrder)
        {
            QuestionChoices = question?.Choices ?? [], QuestionRecommended = question?.RecommendedIndex, // 2026-11-25-9992j
            WorkingOn = SeriesWorkingOn.Of(read.Facts), // 2026-11-25-9992a
            NextMerge = shape.NextMerge, Progress = SeriesProgressWord.Of(shape.Counts.Landed, shape.Counts.Total), // 2026-11-25-433fa
            // 2026-11-24-f9c5c: a merge into a series branch never arms auto-complete; 2026-11-25-9992i: one into the default
            // branch always does — the host's branch protection decides.
            Repos = Repos(project, shape.Tickets), MergeArms = record.IntegrationBranch is null,
            IntegrationBranch = record.IntegrationBranch, CostUsd = spent + record.MasterCostUsd,
            Deliveries = [.. record.Deliveries.Where(d => d.Url.Length > 0).Select(d => new SeriesRunDeliveryView(d.Repo, d.Url, d.Number, d.Said))],
            Review = ReviewModeNames.Of(record.EffectiveReview), // 2026-11-25-9992i
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

    // 2026-11-26-5201e: where each ticket opens in its tracker, as its newest attempt fetched it (the attempt record the ticket
    // list reads it from) — one read for the page; a ticket that never ran has none.
    private async Task<IReadOnlyList<SeriesRunTicketView>> WithTrackerAsync(IReadOnlyList<SeriesRunTicketView> shown, CancellationToken ct)
    {
        var records = await attempts.ForAsync([.. shown.Select(t => t.RunId).OfType<string>().Distinct(StringComparer.Ordinal)], ct);
        return records.Count == 0 ? shown
            : [.. shown.Select(t => t.RunId is { } run && records.GetValueOrDefault(run)?.WebUrl is { Length: > 0 } url ? t with { WebUrl = url } : t)];
    }

    // The repos the series' tickets have pull requests in, in the project's repo order — 2026-11-27-ecf2b: and the lanes they
    // are drawn on, so a spec's repo without a pull request yet takes its place among them.
    private static IReadOnlyList<string> Repos(ResolvedProject project, IReadOnlyList<SeriesRunTicketView> tickets)
    {
        var touched = tickets.SelectMany(t => t.LaneRepo is { } lane ? [.. t.Repos, lane] : t.Repos).ToHashSet(StringComparer.Ordinal);
        var configured = project.Repos.Select(r => r.Name).ToList();
        return [.. configured.Where(touched.Contains), .. touched.Except(configured).Order(StringComparer.Ordinal)];
    }
}
