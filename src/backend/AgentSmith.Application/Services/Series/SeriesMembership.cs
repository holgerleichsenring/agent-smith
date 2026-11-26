using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-24-f9c5b: a filing's tickets join their series BEFORE any of them is started — a filing of two or more opens
/// the series with its branch, a filing seeded with a series id (the series check's follow-up) joins it — so the claim of
/// the first ticket already finds the series and cuts it from the series branch. Run from the filing step's callback
/// between creating and starting; <see cref="OpenSeriesRunHandler"/> then reports it and keeps the follow-up's start.
/// <para>2026-11-25-9992i: the series carries the project's review from its opening; only review: end integrates on a series
/// branch — each and none work their tickets against the default branch.</para>
/// </summary>
public sealed class SeriesMembership(ISeriesRunStore store, TimeProvider clock)
{
    /// <summary>The series a follow-up filed into an existing series is written against, so its ticket branch starts on
    /// the series branch; null for any other filing.</summary>
    public async Task<SeriesRunRecord?> JoiningAsync(PipelineContext pipeline, CancellationToken ct) =>
        pipeline.TryGet<string>(ContextKeys.SeriesRunId, out var id) && !string.IsNullOrEmpty(id)
            ? await store.GetAsync(id!, ct) : null;

    /// <summary>Opens or joins the series for the created tickets; a lone ticket gets a series of one (2026-11-25-96aea).</summary>
    public async Task WriteAsync(PipelineContext pipeline, FilingRequest request, ResolvedProject project,
        IReadOnlyList<FiledTicket> filed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(request);
        var joining = pipeline.TryGet<string>(ContextKeys.SeriesRunId, out var id) && !string.IsNullOrEmpty(id);
        var tickets = OpenSeriesRunHandler.Members(filed, joining && !request.IsSeriesMasterAddition, clock); // 9992f
        if (joining) await store.UpdateAsync(id!, r => OpenSeriesRunHandler.Joined(r, tickets), ct);
        else if (tickets.Count >= 2) pipeline.Set(ContextKeys.SeriesRunOpened, await OpenAsync(request, project, tickets, ct, withBranch: true));
        else if (tickets.Count == 1) // 2026-11-25-96aea: a filing of one is a series of one, talked about in its ticket's own conversation
            pipeline.Set(ContextKeys.SeriesRunOpened, await OpenOneAsync(project, tickets[0], string.Empty, request.Principal, ct, request.MayStartRuns));
    }

    /// <summary>2026-11-25-96aea: opens a series of one over the ticket — no series branch, the project's review with end
    /// becoming each (one flow with one ticket); answers its id.</summary>
    public async Task<string> OpenOneAsync(ResolvedProject project, SeriesTicket ticket, string conversation, string principal,
        CancellationToken ct, bool mayStartRuns = true, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        id ??= SeriesRunIds.Mint();
        await store.OpenAsync(new SeriesRunRecord(id, conversation, project.Name, principal, mayStartRuns, SeriesRunStates.Running,
            [ticket], clock.GetUtcNow()) { Review = Review(project.Review, null) }, ct);
        return id;
    }

    /// <summary>Opens a series over the tickets under the project's review; answers its id. With <paramref name="withBranch"/>
    /// and review: end it integrates on its own branch — only when no ticket of it started yet, since a started one already
    /// works against the default branch.</summary>
    public async Task<string> OpenAsync(FilingRequest request, ResolvedProject project, IReadOnlyList<SeriesTicket> tickets,
        CancellationToken ct, bool withBranch)
    {
        ArgumentNullException.ThrowIfNull(project);
        var id = SeriesRunIds.Mint();
        var branch = withBranch && project.Review == ReviewMode.End ? TicketBranchNamer.Series(id).Value : null;
        await store.OpenAsync(new SeriesRunRecord(id, request.Conversation, project.Name, request.Principal, request.MayStartRuns,
            SeriesRunStates.Running, tickets, clock.GetUtcNow()) { IntegrationBranch = branch, Review = Review(project.Review, branch) }, ct);
        return id;
    }

    // 2026-11-25-9992i: an end series that cannot have its branch (its tickets started already) is reviewed per ticket.
    private static ReviewMode Review(ReviewMode review, string? branch) =>
        review == ReviewMode.End && branch is null ? ReviewMode.Each : review;
}
