using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: a beat's first step — the series record named by the beat's seed, its tickets' facts, and what
/// needs the master's judgement, handed on as one <see cref="SeriesBeat"/>. A series that ended since the beat was
/// launched ends the beat quietly: nothing is said and nothing acts.
/// </summary>
public sealed class ReadSeriesHandler(
    ISeriesRunStore store, ISeriesTicketFacts facts, ISeriesConversation conversation, AgentSmithConfig config, TimeProvider clock,
    SeriesQuestions questions, SeriesProgress progress)
    : ICommandHandler<ReadSeriesContext>
{
    public async Task<CommandResult> ExecuteAsync(ReadSeriesContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<string>(ContextKeys.SeriesRunId, out var id) || string.IsNullOrEmpty(id))
            return CommandResult.Fail($"A series-run beat is started with ContextKeys.{nameof(ContextKeys.SeriesRunId)}; this one has none.");
        var record = await store.GetAsync(id!, cancellationToken);
        if (record is null) return CommandResult.Fail($"Series run {id} does not exist.");
        if (!record.IsActive) return CommandResult.Ok($"Series run {id} is {record.State}; this beat has nothing to do.");
        record = await TicketConversationAsync(context, record, cancellationToken);
        var beat = new SeriesBeat(record, await facts.ReadAsync(record, cancellationToken), clock.GetUtcNow())
        {
            Actor = context.Project.ActorName, // 2026-11-25-9992h
        };
        beat.Messages.AddRange(await conversation.AddressedAsync(record.Conversation, record.Id, record.MessagesThrough, cancellationToken));
        if (beat.Messages.Count > 0) beat.MessagesThrough = beat.Messages[^1].Index;
        await questions.SettleAsync(beat, cancellationToken); // 0207c; 2026-11-27-3532b: a run's question is a reason
        await progress.MoveAsync(beat, cancellationToken); // 0207d: the series check, its report, the series' end
        beat.Reasons.AddRange(SeriesJudgement.NewlyRed(record, beat.Rows));
        beat.Reasons.AddRange(SeriesJudgement.NewlyRetarget(record, beat)); // 2026-11-26-5201f
        if (!SeriesBeatDue.PersonMerges(record)) beat.Reasons.AddRange(SeriesJudgement.NewlyNext(record, beat.Rows)); // 96aec, 96aea
        beat.Reasons.AddRange(SeriesJudgement.Operator(beat.Messages));
        HandOver(beat, record); // 2026-11-25-9992g, 96aec
        context.Pipeline.Set(ContextKeys.SeriesBeat, beat);
        return CommandResult.Ok($"Series run {id}: {beat.Facts.Count(f => f.Landed)} of {beat.Facts.Count} landed"
            + (beat.Reasons.Count == 0 ? string.Empty : $"; to judge: {string.Join("; ", beat.Reasons)}"));
    }

    // 2026-11-27-ecf2e (operator): a series of one talks in its ticket's conversation — the ticket page's chat — once one is
    // bound to the ticket; the master asks there and the operator answers there.
    private async Task<SeriesRunRecord> TicketConversationAsync(ReadSeriesContext context, SeriesRunRecord record, CancellationToken ct)
    {
        if (record is not { IsOne: true, Conversation: "" } || record.Tickets.Count == 0) return record;
        if (await conversation.TicketConversationAsync(context.Project, record.Tickets[0].TicketId, ct) is not { Length: > 0 } talk)
            return record;
        return await store.UpdateAsync(record.Id, r => r with { Conversation = talk }, ct) ?? record with { Conversation = talk };
    }

    // 2026-11-25-96aec: the events that need a decision go to the master once they settled, or at once when the beat has a
    // reason of its own; the rest ride along only with a reason. A paused series keeps them all (9992g) — and they are never
    // an answer to the waiting question, which was settled above.
    private void HandOver(SeriesBeat beat, SeriesRunRecord record)
    {
        if (record.State == SeriesRunStates.Paused) return;
        var due = record.Wakes.Where(w => w.At <= beat.Now).ToList();
        var waking = due.Where(w => SeriesBeatDue.Waking(record, w)).ToList();
        if (waking.Count > 0 && (beat.Reasons.Count > 0 || SeriesBeatDue.Woken(record, config.Dialogue, beat.Now)))
        {
            beat.Wakes.AddRange(waking);
            beat.Reasons.AddRange(SeriesJudgement.Woken(beat, waking));
        }
        if (beat.Reasons.Count == 0) return;
        var recorded = due.Except(waking).ToList();
        beat.Wakes.AddRange(recorded);
        beat.Reasons.AddRange(SeriesJudgement.Recorded(beat, recorded));
    }
}
