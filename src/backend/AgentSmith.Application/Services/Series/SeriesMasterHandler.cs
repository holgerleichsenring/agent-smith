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

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: the series master's loop, run only when the beat has something to judge. It reads the series
/// through its tools, acts through the paths a person's act takes, and its answer is the line the beat posts. Its
/// thread continues across beats; its spend accrues on the record, and once dialogue.series_master_budget_usd is
/// spent it judges no more and the conversation is told once.
/// </summary>
public sealed class SeriesMasterHandler(
    IAgenticLoopRunner loop, IPromptCatalog prompts, ISeriesTicketActs acts, ISeriesRunStore store, AgentSmithConfig config,
    ISeriesConversation conversation)
    : ICommandHandler<SeriesMasterContext>
{
    public const string MasterName = "series-master";

    public async Task<CommandResult> ExecuteAsync(SeriesMasterContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<SeriesBeat>(ContextKeys.SeriesBeat, out var beat) || beat is null)
            return CommandResult.Ok("No series to judge.");
        if (beat.Reasons.Count == 0) return CommandResult.Ok("Nothing needs the series master's judgement.");
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
        IList<AITool> tools = [.. new SeriesReadTools(beat).GetTools().Concat(new SeriesActTools(beat, acts, store).GetTools())
            .Select(f => new MidTurnMessageAIFunction(f, messages))];
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
                + "running and the status keeps coming. Raise the setting to let it judge again.");
        }
        return CommandResult.Ok($"The series master's budget of {budget} USD (dialogue.series_master_budget_usd) is spent.");
    }
}
