using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Series;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// p0461, p0327: the answers the resume sweep takes when the inbox holds none — the operator's reply on the ticket (asked
/// BEFORE the deadline default, so an answer on the work item beats the headless fallback), then the persisted default once
/// the deadline elapsed, each written through the SAME first-wins inbox so a racing real answer wins.
/// <para>2026-11-27-3532b: a series-led run's question is its series master's (<see cref="SeriesLedRun"/>): neither answers
/// it — a tracker comment wakes the master as a comment, and the passed deadline wakes it too (<see cref="SeriesQuestionDeadline"/>).
/// A run whose deadline its master was handed takes no default later either, once its series stops leading: a person answers it.</para>
/// </summary>
public sealed class ParkedAnswerFallback(
    IParkedTicketDialogue ticket, IDialogueAnswerInbox inbox, SeriesLedRun led, SeriesQuestionDeadline deadlines,
    TimeProvider timeProvider, ILogger<ParkedAnswerFallback> logger)
{
    public async Task<DialogAnswer?> TakeAsync(RunCheckpointRecord checkpoint, SeriesLedScan scan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(scan);
        var series = await scan.NewestAsync(checkpoint.Project, checkpoint.TicketId, ct);
        if (led.Leads(series))
        {
            await deadlines.WakeIfPassedAsync(checkpoint, series!, ct);
            return null;
        }
        return await FromTicketAsync(checkpoint, ct)
            ?? (SeriesQuestionDeadline.Handed(series, checkpoint) ? null : await ApplyDeadlineDefaultAsync(checkpoint, ct));
    }

    private async Task<DialogAnswer?> FromTicketAsync(RunCheckpointRecord checkpoint, CancellationToken ct) =>
        await ticket.TryCollectAnswerAsync(checkpoint, ct)
            ? await inbox.GetAsync(checkpoint.DialogueJobId, checkpoint.QuestionId, ct)
            : null;

    private async Task<DialogAnswer?> ApplyDeadlineDefaultAsync(RunCheckpointRecord checkpoint, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        if (now < checkpoint.AnswerDeadlineAt) return null;
        var question = System.Text.Json.JsonSerializer.Deserialize<DialogQuestion>(checkpoint.QuestionJson);
        var fallback = new DialogAnswer(checkpoint.QuestionId, question?.DefaultAnswer ?? "", "timeout", now, "system");
        await inbox.TryDeliverAsync(checkpoint.DialogueJobId, fallback, ct);
        logger.LogWarning("Run {RunId} answer deadline elapsed — applying default answer '{Default}' headless",
            checkpoint.RunId, fallback.Answer);
        return await inbox.GetAsync(checkpoint.DialogueJobId, checkpoint.QuestionId, ct);
    }
}
