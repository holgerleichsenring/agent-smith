using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-27-3532b: the series master's tell_ticket. A ticket whose newest run waits on a checkpointed question gets the
/// text as that question's answer, through the same first-answer-wins inbox every answer takes — the resume sweep resumes
/// the run and its intake reads free text as well as a choice. With a question id it answers only that question: a run that
/// was retried and waits on another is named, and nothing is answered. Any other ticket gets the text in its conversation,
/// as before (<see cref="SeriesTicketMessage"/>).
/// </summary>
public sealed class SeriesTicketAnswer(
    IPreviousAttemptReader attempts, IRunCheckpointStore checkpoints, IDialogueAnswerInbox inbox, SeriesTicketMessage messages,
    TimeProvider clock)
{
    public async Task<string> TellAsync(SeriesTell tell, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tell);
        if (await ParkedAsync(tell.Project, tell.TicketId, ct) is not { } checkpoint)
            return await messages.SendAsync(tell.Project, tell.TicketId, tell.Text, tell.Principal, tell.MayStartRuns, ct);
        if (tell.QuestionId is { Length: > 0 } asked && asked != checkpoint.QuestionId) return Moved(tell, checkpoint);
        var answer = new DialogAnswer(checkpoint.QuestionId, tell.Text.Trim(), null, clock.GetUtcNow(), tell.Principal);
        return await inbox.TryDeliverAsync(checkpoint.DialogueJobId, answer, ct)
            ? $"Answered {tell.TicketKey}'s question {checkpoint.QuestionId}; its run resumes with your answer."
            : $"{tell.TicketKey}'s question {checkpoint.QuestionId} was already answered; its run resumes with that answer, not this one.";
    }

    private static string Moved(SeriesTell tell, RunCheckpointRecord checkpoint) =>
        $"{tell.TicketKey}'s run no longer waits on question {tell.QuestionId}; it waits on question {checkpoint.QuestionId}: "
        + $"\"{JsonSerializer.Deserialize<DialogQuestion>(checkpoint.QuestionJson)?.Text}\". Nothing was answered.";

    private async Task<RunCheckpointRecord?> ParkedAsync(string project, string ticketId, CancellationToken ct) =>
        await attempts.LatestAsync(project, ticketId, null, ct) is { Status: RunStatuses.WaitingForInput } latest
            && await checkpoints.GetByRunIdAsync(latest.RunId, ct) is { ResumedAt: null } checkpoint
            ? checkpoint : null;
}
