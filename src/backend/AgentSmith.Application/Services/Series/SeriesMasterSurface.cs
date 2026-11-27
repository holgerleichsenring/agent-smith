using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-27-3532c: the series master's tools for one beat — its reads of the series, its acts (each logged with its
/// reason), the record reads it is lent and the design turn's source reads over the beat's scopes. Every tool hands on
/// what the operator wrote meanwhile. Extracted from <see cref="SeriesMasterHandler"/>.
/// </summary>
public sealed class SeriesMasterSurface(
    ISeriesTicketActs acts, ISeriesRunStore store, AgentSmithConfig config, ISeriesConversation conversation,
    SeriesProgress progress, SeriesPause pause, ISeriesPullRequestActs pullRequests, SeriesFinalMerge finals,
    SeriesFinalCi finalCi, ISeriesEditActs edits, ILogger logger, ISeriesRecordReads? records, TimeProvider? clock,
    IRunReportReader? reports)
{
    public IList<AITool> Tools(SeriesBeat beat, SeriesMasterContext context, SeriesSourceMap sources)
    {
        var messages = new SeriesMidTurnMessages(beat, conversation); // 0207b: what the operator writes meanwhile
        var series = new SeriesReadTools(beat, reports).GetTools().Concat(Acted(beat, context));
        // 3532c: the source reads come last, unbounded as a design turn has them, so the tools before them keep their order
        return [.. series.Select(f => new MidTurnMessageAIFunction(f, messages)), .. records?.Tools() ?? [],
            .. sources.ReadTools(logger).Select(f => new MidTurnMessageAIFunction(f, messages))];
    }

    // 2026-11-25-e94ec: an act marks the beat, whose reply is posted only then (SeriesBeat.MasterSpeaks); a read does not.
    private IEnumerable<AIFunction> Acted(SeriesBeat beat, SeriesMasterContext context)
    {
        var ask = new SeriesAskTool(beat, store, config.Dialogue.QuestionDefaultMinutes).GetTool(); // 0207c
        var check = AIFunctionFactory.Create(
            ([System.ComponentModel.Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default) =>
                progress.CheckAsync(beat, ct), "check_series", // 0207d; 2026-11-25-9992e: with the reason logged
            "Starts the series check again (on the series branch when it has one, else the default branch) — for a check that ended without a verdict.");
        var prs = new SeriesPullRequestTools(beat, context, pullRequests, finals, finalCi).GetTools(); // 2026-11-25-9992e
        var cut = new SeriesEditTools(beat, edits, store).GetTools(); // 2026-11-25-9992f
        return new SeriesActTools(beat, acts, store, pause).GetTools().Append(ask).Append(check).Concat(prs).Concat(cut)
            .Select(f => (AIFunction)new SeriesActedFunction(f, beat, store, logger, clock));
    }
}
