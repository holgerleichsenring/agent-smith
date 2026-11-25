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
/// its reason (<see cref="SeriesActedFunction"/>); its surface stays within the house cap of 20 tools.
/// 2026-11-25-9992g: the events on its tickets wake it (<see cref="SeriesBeat.Wakes"/>), cleared once judged; before its
/// budget is spent the conversation is asked to raise it or stop (<see cref="SeriesBudgetQuestion"/>).
/// </summary>
public sealed class SeriesMasterHandler(
    IAgenticLoopRunner loop, IPromptCatalog prompts, ISeriesTicketActs acts, ISeriesRunStore store, AgentSmithConfig config,
    ISeriesConversation conversation, SeriesProgress progress, SeriesPause pause, ISeriesPullRequestActs pullRequests,
    SeriesFinalMerge finals, SeriesFinalCi finalCi, SeriesBudgetQuestion budgets, // 2026-11-25-9992g
    ILogger<SeriesMasterHandler> logger, ISeriesRecordReads? records = null)
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
        var prompt = SeriesMasterPrompt.Build(beat);
        var tracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        var before = tracker.CaptureSpend().AccruedUsd;
        AgenticLoopResult result;
        try { result = await loop.RunAsync(Request(context, beat, prompt), ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FailedAsync(beat, ex, tracker.CaptureSpend().AccruedUsd - before, ct);
        }
        tracker.Track(result.Response);
        await KeepAsync(beat, prompt, result.Response.Text, tracker.CaptureSpend().AccruedUsd - before, ct);
        await budgets.AskIfLowAsync(beat, ct); // 2026-11-25-9992g: this judgement may have spent it down
        return CommandResult.Ok($"The series master judged: {string.Join("; ", beat.Reasons)}");
    }

    // 2026-11-25-9992g: a failed loop keeps its wakes and the spend it made; they wait a status period, not the next sweep.
    private async Task<CommandResult> FailedAsync(SeriesBeat beat, Exception ex, decimal spent, CancellationToken ct)
    {
        logger.LogWarning(ex, "Series {Series}: the master's loop failed", beat.Record.Id);
        if (await store.UpdateAsync(beat.Record.Id, r => r with { MasterFailedAt = beat.Now, MasterCostUsd = r.MasterCostUsd + spent }, ct)
            is { } kept) beat.Record = kept;
        var minutes = config.Dialogue.SeriesStatusMinutes;
        return CommandResult.Fail($"The series master's loop failed ({ex.Message}); its events wait {minutes} minutes "
            + "(dialogue.series_status_minutes, the stall cadence) so a failing model is not retried every sweep.");
    }

    private AgenticLoopRequest Request(SeriesMasterContext context, SeriesBeat beat, string prompt)
    {
        var messages = new SeriesMidTurnMessages(beat, conversation); // 0207b: what the operator writes meanwhile
        var ask = new SeriesAskTool(beat, store, config.Dialogue.QuestionDefaultMinutes).GetTool(); // 0207c
        var check = AIFunctionFactory.Create(
            ([System.ComponentModel.Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default) =>
                progress.CheckAsync(beat, ct), "check_series", // 0207d; 2026-11-25-9992e: with the reason logged
            "Starts the series check again (on the series branch when it has one, else the default branch) — for a check that ended without a verdict.");
        // 2026-11-25-e94ec: an act marks the beat, whose reply is posted only then (SeriesBeat.MasterSpeaks); a read does not.
        var prs = new SeriesPullRequestTools(beat, context, pullRequests, finals, finalCi).GetTools(); // 2026-11-25-9992e
        var acted = new SeriesActTools(beat, acts, store, pause).GetTools().Append(ask).Append(check).Concat(prs)
            .Select(f => (AIFunction)new SeriesActedFunction(f, beat, store, logger));
        IList<AITool> tools = [.. new SeriesReadTools(beat).GetTools().Concat(acted)
            .Select(f => new MidTurnMessageAIFunction(f, messages)), .. records?.Tools() ?? []];
        var agent = context.Project.Agent;
        return new AgenticLoopRequest(agent, TaskType.Primary, prompts.Get(MasterName), prompt, tools,
            Name: MasterName, PriorMessages: SeriesThread.Read(beat.Record.ThreadJson), Compaction: agent.Compaction);
    }

    private async Task KeepAsync(SeriesBeat beat, string prompt, string answer, decimal spent, CancellationToken ct)
    {
        beat.MasterLine = string.IsNullOrWhiteSpace(answer) ? null : answer.Trim();
        var kept = await store.UpdateAsync(beat.Record.Id, r => r with
        {
            ThreadJson = SeriesThread.Append(r.ThreadJson, prompt, answer), MasterCostUsd = r.MasterCostUsd + spent,
            Wakes = beat.Unjudged(r.Wakes), MasterFailedAt = null, // 2026-11-25-9992g: exactly the wakes it judged
        }, ct);
        if (kept is not null) beat.Record = kept;
    }
}
