using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-433fb: a beat's last step — keeps the series master oriented over days within its context and cost. Once its
/// stored thread passes dialogue.series_master_thread_max_chars, the master writes notes (what it decided and why, what is
/// still open, the acts it took, its plan) in place of its older beats; the latest dialogue.series_master_thread_keep_turns
/// beats stay verbatim. The series' tickets are re-read every beat, so the notes hold reasoning, not state. A failed or empty
/// note keeps the thread as it was, and the next judged beat tries again; a spent budget compacts nothing.
/// </summary>
public sealed class CompactSeriesThreadHandler(
    IAgenticLoopRunner loop, ISeriesRunStore store, AgentSmithConfig config, SeriesBudgetQuestion budgets,
    ILogger<CompactSeriesThreadHandler> logger) : ICommandHandler<CompactSeriesThreadContext>
{
    public const string NotesName = "series-master-notes";

    public const string SystemPrompt = "You are the series master of an agent-smith series. You run in short beats and keep a "
        + "thread between them; that thread has grown long, and you now write the notes your later beats read in its place.";

    public const string Instruction = "Write your notes in place of the beats above. Say what you decided and why, which "
        + "questions are still open and whom they wait on, the acts you took and their reasons, and your plan from here. The "
        + "series' tickets and their states are read for you again at every beat, so leave them out. Answer with the notes only.";

    public async Task<CommandResult> ExecuteAsync(CompactSeriesThreadContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<SeriesBeat>(ContextKeys.SeriesBeat, out var beat) || beat is null || !beat.Record.IsActive
            || !beat.MasterJudged) return CommandResult.Ok("The master did not judge this beat; its thread did not grow.");
        var max = config.Dialogue.SeriesMasterThreadMaxChars;
        var json = beat.Record.ThreadJson;
        var (size, older) = (SeriesThread.Size(json), SeriesThread.Pairs(json) - Keep);
        // Only an older part worth half the bound is folded: latest beats that alone outgrow it are not re-folded every beat.
        if (max <= 0 || size <= max || older < 1 || SeriesThread.Size(json, older) < max / 2)
            return CommandResult.Ok($"The master's thread is {size} characters (dialogue.series_master_thread_max_chars {max}); kept.");
        if (budgets.Spent(beat.Record)) return CommandResult.Ok("The series master's budget is spent; its thread is kept as it is.");
        return await CompactAsync(context, beat, size, older, cancellationToken);
    }

    private async Task<CommandResult> CompactAsync(
        CompactSeriesThreadContext context, SeriesBeat beat, int size, int older, CancellationToken ct)
    {
        var tracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        var before = tracker.CaptureSpend().AccruedUsd;
        var notes = await NotesAsync(context, beat, older, tracker, ct);
        var spent = tracker.CaptureSpend().AccruedUsd - before;
        if (await store.UpdateAsync(beat.Record.Id, r => r with
            {
                ThreadJson = notes is null ? r.ThreadJson : SeriesThread.Compact(r.ThreadJson, older, notes),
                MasterCostUsd = r.MasterCostUsd + spent,
            }, ct) is { } kept) beat.Record = kept;
        if (notes is null) return CommandResult.Ok("The series master wrote no notes; its thread is kept and compacted at a later beat.");
        var said = Said(beat.Record.Id, size, older);
        logger.LogInformation("{Said}", said);
        return CommandResult.Ok(said);
    }

    // The master's notes over its older beats; null when the call failed or answered nothing.
    private async Task<string?> NotesAsync(
        CompactSeriesThreadContext context, SeriesBeat beat, int older, PipelineCostTracker tracker, CancellationToken ct)
    {
        var agent = context.Project.Agent;
        try
        {
            var result = await loop.RunAsync(new AgenticLoopRequest(agent, TaskType.Primary, SystemPrompt, Instruction, [],
                Name: NotesName, PriorMessages: SeriesThread.Older(beat.Record.ThreadJson, older), Compaction: agent.Compaction), ct);
            tracker.Track(result.Response);
            return string.IsNullOrWhiteSpace(result.Response.Text) ? null : result.Response.Text.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Series {Series}: the master's notes failed; its thread is kept", beat.Record.Id);
            return null;
        }
    }

    private int Keep => Math.Max(0, config.Dialogue.SeriesMasterThreadKeepTurns);

    private string Said(string series, int size, int older) =>
        $"Series {series}: the master's thread reached {size} characters, past dialogue.series_master_thread_max_chars "
        + $"({config.Dialogue.SeriesMasterThreadMaxChars}), the bound that keeps a series running for days inside the master's "
        + $"context and cost; its {older} older entries (earlier notes among them) became its notes, the latest {Keep} beats "
        + "stay (dialogue.series_master_thread_keep_turns).";
}
