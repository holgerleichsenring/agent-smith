using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-24-f9c5b: a filing's tickets join their series BEFORE any of them is started — a filing of two or more opens
/// the series with its branch, a filing seeded with a series id (the series check's follow-up) joins it — so the claim of
/// the first ticket already finds the series and cuts it from the series branch. Run from the filing step's callback
/// between creating and starting; <see cref="OpenSeriesRunHandler"/> then reports it and keeps the follow-up's start.
/// </summary>
public sealed class SeriesMembership(ISeriesRunStore store, TimeProvider clock)
{
    /// <summary>The series a follow-up filed into an existing series is written against, so its ticket branch starts on
    /// the series branch; null for any other filing.</summary>
    public async Task<SeriesRunRecord?> JoiningAsync(PipelineContext pipeline, CancellationToken ct) =>
        pipeline.TryGet<string>(ContextKeys.SeriesRunId, out var id) && !string.IsNullOrEmpty(id)
            ? await store.GetAsync(id!, ct) : null;

    /// <summary>Opens or joins the series for the created tickets; nothing for a lone ticket outside a series.</summary>
    public async Task WriteAsync(PipelineContext pipeline, FilingRequest request, string project,
        IReadOnlyList<FiledTicket> filed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(request);
        var joining = pipeline.TryGet<string>(ContextKeys.SeriesRunId, out var id) && !string.IsNullOrEmpty(id);
        var tickets = OpenSeriesRunHandler.Members(filed, joining, clock);
        if (joining) await store.UpdateAsync(id!, r => OpenSeriesRunHandler.Joined(r, tickets), ct);
        else if (tickets.Count >= 2) pipeline.Set(ContextKeys.SeriesRunOpened, await OpenAsync(request, project, tickets, ct, withBranch: true));
    }

    /// <summary>Opens a series over the tickets; answers its id. With <paramref name="withBranch"/> it integrates on its own
    /// branch — only when no ticket of it started yet, since a started one already works against the default branch.</summary>
    public async Task<string> OpenAsync(FilingRequest request, string project, IReadOnlyList<SeriesTicket> tickets, CancellationToken ct,
        bool withBranch)
    {
        var id = SeriesRunIds.Mint();
        await store.OpenAsync(new SeriesRunRecord(id, request.Conversation, project, request.Principal, request.MayStartRuns,
            SeriesRunStates.Running, tickets, clock.GetUtcNow()) { IntegrationBranch = withBranch ? TicketBranchNamer.Series(id).Value : null }, ct);
        return id;
    }
}
