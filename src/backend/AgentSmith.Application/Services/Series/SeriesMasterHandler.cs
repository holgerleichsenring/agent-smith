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
/// </summary>
public sealed class SeriesMasterHandler(
    IAgenticLoopRunner loop, IPromptCatalog prompts, ISeriesTicketActs acts, ISeriesRunStore store, AgentSmithConfig config,
    ISeriesConversation conversation, SeriesProgress progress, SeriesPause pause, ISeriesPullRequestActs pullRequests,
    SeriesFinalMerge finals, SeriesFinalCi finalCi, ILogger<SeriesMasterHandler> logger, ISeriesRecordReads? records = null)
    : ICommandHandler<SeriesMasterContext>
{
    public const string MasterName = "series-master";

    public async Task<CommandResult> ExecuteAsync(SeriesMasterContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<SeriesBeat>(ContextKeys.SeriesBeat, out var beat) || beat is null)
            return CommandResult.Ok("No series to judge.");
        if (beat.Reasons.Count == 0) return CommandResult.Ok("Nothing needs the series master's judgement.");
        // 2026-11-25-9992e: a series that ended in this beat (its final pull requests merged) has nothing left to judge.
        if (!beat.Record.IsActive) return CommandResult.Ok($"The series is {beat.Record.State}; nothing is left to judge.");
        var budget = config.Dialogue.SeriesMasterBudgetUsd;
        if (beat.Record.MasterCostUsd >= budget) return await SpentAsync(beat, budget, cancellationToken);
        var prompt = SeriesMasterPrompt.Build(beat);
        var tracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        var before = tracker.CaptureSpend().AccruedUsd;
        var result = await loop.RunAsync(Request(context, beat, prompt), cancellationToken);
        tracker.Track(result.Response);
        await KeepAsync(beat, prompt, result.Response.Text, tracker.CaptureSpend().AccruedUsd - before, cancellationToken);
        return CommandResult.Ok($"The series master judged: {string.Join("; ", beat.Reasons)}");
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
        }, ct);
        if (kept is not null) beat.Record = kept;
    }

    private async Task<CommandResult> SpentAsync(SeriesBeat beat, decimal budget, CancellationToken ct)
    {
        if (!beat.Record.BudgetSaid
            && await store.UpdateAsync(beat.Record.Id, r => r with { BudgetSaid = true }, ct) is { } said)
        {
            beat.Record = said;
            beat.Said.Add($"The series master has spent {SeriesTable.Money(said.MasterCostUsd)} of its "
                + $"{SeriesTable.Money(budget)} (dialogue.series_master_budget_usd), so it judges no more; the tickets keep "
                + "running. Raise the setting to let it judge again.");
        }
        return CommandResult.Ok($"The series master's budget of {budget} USD (dialogue.series_master_budget_usd) is spent.");
    }
}
