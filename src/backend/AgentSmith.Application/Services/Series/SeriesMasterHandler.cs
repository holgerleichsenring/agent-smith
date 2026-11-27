using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: the series master's loop, run only when the beat has something to judge. It reads the series
/// through its tools, acts through the paths a person's act takes, and its answer is the line the beat posts. Its
/// thread continues across beats; its spend accrues on the record, and once dialogue.series_master_budget_usd is
/// spent it judges no more and the conversation is told once. 2026-11-25-9992e: it merges, refreshes, retargets and reads
/// or restarts CI for its tickets and the final pull requests (<see cref="SeriesPullRequestTools"/>); every act is logged with
/// its reason (<see cref="SeriesActedFunction"/>). 2026-11-27-3532c: it reads its project's repositories, their specs and its
/// conversation's uploads (<see cref="ISeriesSourceScopes"/>), its tools composed by <see cref="SeriesMasterSurface"/>; it is
/// the one master outside the 2026-11-18-9c1cd cap of 20 tools (the operator: no counts, no caps).
/// 2026-11-25-9992g: the events on its tickets wake it (<see cref="SeriesBeat.Wakes"/>), cleared once judged; before its
/// budget is spent the conversation is asked to raise it or stop (<see cref="SeriesBudgetQuestion"/>).
/// 2026-11-25-9992f: it adds tickets and re-links requirements itself (<see cref="SeriesEditTools"/>).
/// </summary>
public sealed class SeriesMasterHandler(
    IAgenticLoopRunner loop, IPromptCatalog prompts, ISeriesTicketActs acts, ISeriesRunStore store, AgentSmithConfig config,
    ISeriesConversation conversation, SeriesProgress progress, SeriesPause pause, ISeriesPullRequestActs pullRequests,
    SeriesFinalMerge finals, SeriesFinalCi finalCi, SeriesBudgetQuestion budgets, // 2026-11-25-9992g
    ISeriesEditActs edits, // 2026-11-25-9992f
    ILogger<SeriesMasterHandler> logger, ISeriesRecordReads? records = null, TimeProvider? clock = null,
    AgentSmith.Contracts.Runs.IRunReportReader? reports = null, // 2026-11-26-a12ed
    ISeriesSourceScopes? sources = null, // 2026-11-27-3532c
    ISeriesSpecRetirement? retirement = null) // 2026-11-27-3532d
    : ICommandHandler<SeriesMasterContext>
{
    public const string MasterName = "series-master";

    public async Task<CommandResult> ExecuteAsync(SeriesMasterContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<SeriesBeat>(ContextKeys.SeriesBeat, out var beat) || beat is null)
            return CommandResult.Ok("No series to judge.");
        // 2026-11-25-9992e: a series that ended in this beat (its final pull requests merged) has nothing left to judge.
        if (!beat.Record.IsActive) return CommandResult.Ok($"The series is {beat.Record.State}; nothing is left to judge.");
        await budgets.AskIfLowAsync(beat, cancellationToken); // 2026-11-25-9992g: in any beat, once it runs low
        if (beat.Reasons.Count == 0) return CommandResult.Ok("Nothing needs the series master's judgement.");
        if (budgets.Spent(beat.Record)) return CommandResult.Ok(await budgets.SpentAsync(beat, cancellationToken));
        return await JudgeAsync(context, beat, cancellationToken);
    }

    private async Task<CommandResult> JudgeAsync(SeriesMasterContext context, SeriesBeat beat, CancellationToken ct)
    {
        var tracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        var before = tracker.CaptureSpend().AccruedUsd;
        AgenticLoopResult result;
        // 2026-11-27-3532c: the beat owns its read scopes; a failure opening them takes the failed loop's back-off, and they
        // are disposed on every path, a cancelled one's too.
        var readable = SeriesSourceMap.None;
        try
        {
            if (sources is not null) readable = await sources.OpenAsync(beat, context.Project, ct);
            result = await loop.RunAsync(Request(context, beat, SeriesMasterPrompt.Build(beat, readable.Sources), readable), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FailedAsync(beat, ex, tracker.CaptureSpend().AccruedUsd - before, ct);
        }
        finally { await readable.DisposeAsync(); }
        tracker.Track(result.Response);
        // 2026-11-25-433fb: the thread keeps the turn without its table; CompactSeriesThread bounds what it keeps
        await KeepAsync(beat, SeriesMasterPrompt.Kept(beat), result.Response.Text, tracker.CaptureSpend().AccruedUsd - before, ct);
        await budgets.AskIfLowAsync(beat, ct); // 2026-11-25-9992g: this judgement may have spent it down
        return CommandResult.Ok($"The series master judged: {string.Join("; ", beat.Reasons)}");
    }

    // 2026-11-25-9992g: a failed loop keeps its wakes and the spend it made; they wait dialogue.series_status_minutes, not the next sweep.
    private async Task<CommandResult> FailedAsync(SeriesBeat beat, Exception ex, decimal spent, CancellationToken ct)
    {
        logger.LogWarning(ex, "Series {Series}: the master's loop failed", beat.Record.Id);
        if (await store.UpdateAsync(beat.Record.Id, r => r with { MasterFailedAt = beat.Now, MasterCostUsd = r.MasterCostUsd + spent }, ct)
            is { } kept) beat.Record = kept;
        var minutes = config.Dialogue.SeriesStatusMinutes;
        return CommandResult.Fail($"The series master's loop failed ({ex.Message}); its events wait {minutes} minutes "
            + "(dialogue.series_status_minutes) so a failing model is not retried every sweep.");
    }

    private AgenticLoopRequest Request(SeriesMasterContext context, SeriesBeat beat, string prompt, SeriesSourceMap readable)
    {
        var tools = new SeriesMasterSurface(acts, store, config, conversation, progress, pause, pullRequests, finals, finalCi,
            edits, logger, records, clock, reports, retirement).Tools(beat, context, readable); // 2026-11-27-3532c
        var agent = context.Project.Agent;
        return new AgenticLoopRequest(agent, TaskType.Primary, prompts.Get(MasterName), prompt, tools,
            Name: MasterName, PriorMessages: SeriesThread.Read(beat.Record.ThreadJson), Compaction: agent.Compaction);
    }

    private async Task KeepAsync(SeriesBeat beat, string prompt, string answer, decimal spent, CancellationToken ct)
    {
        beat.MasterLine = string.IsNullOrWhiteSpace(answer) ? null : answer.Trim();
        beat.MasterJudged = true; // 2026-11-25-433fb: its thread grew; CompactSeriesThread looks at it
        var kept = await store.UpdateAsync(beat.Record.Id, r => r with
        {
            ThreadJson = SeriesThread.Append(r.ThreadJson, prompt, answer), MasterCostUsd = r.MasterCostUsd + spent,
            Wakes = beat.Unjudged(r.Wakes), MasterFailedAt = null, // 2026-11-25-9992g: exactly the wakes it judged
            Raised = [.. r.Raised, .. beat.Raised.Where(k => !r.Raised.Contains(k))], // 2026-11-25-96aec
            // 2026-11-27-3532b: the answer it was handed this beat reached it — as its wakes, only once it judged
            Question = beat.MasterAnswered && r.Question is { Answer: not null, Delivered: false } handed ? handed with { Delivered = true } : r.Question,
        }, ct);
        if (kept is not null) beat.Record = kept;
    }
}
