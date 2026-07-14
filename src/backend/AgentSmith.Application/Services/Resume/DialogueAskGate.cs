using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// p0327: implements the hybrid wait. Order of resolution:
/// 1. a resume-delivered answer (consumed exactly once),
/// 2. the hot in-memory wait (full timeout when the run cannot checkpoint),
/// 3. checkpoint + park for eligible ticket runs past the hot threshold,
/// 4. the question's DefaultAnswer (timeout / no transport identity).
/// The dialogue identity falls back to the run id so in-process server runs
/// (no --job-id) still have working question/answer streams.
/// </summary>
public sealed class DialogueAskGate(
    IDialogueTransport dialogueTransport,
    IDialogueTrail dialogueTrail,
    IDialogueCheckpointWriter checkpointWriter,
    IProgressReporter progressReporter,
    ILogger<DialogueAskGate> logger) : IDialogueAskGate
{
    private const int DefaultHotWaitSeconds = 600;

    public async Task<DialogueAskOutcome> AskAsync(
        PipelineContext pipeline, DialogQuestion question, CancellationToken cancellationToken)
    {
        if (TryConsumeResumedAnswer(pipeline, question, out var resumed))
            return await RecordAsync(question, resumed!);

        var jobId = ResolveDialogueJobId(pipeline);
        if (jobId is null)
        {
            logger.LogWarning("No dialogue identity available — using default answer for '{QuestionId}'",
                question.QuestionId);
            return await RecordAsync(question, DefaultAnswerFor(question, "no job ID"));
        }

        await dialogueTransport.PublishQuestionAsync(jobId, question, cancellationToken);
        var eligible = IsCheckpointEligible(pipeline, question, out var hotWindow);
        var answer = await dialogueTransport.WaitForAnswerAsync(
            jobId, question.QuestionId, hotWindow, cancellationToken);
        if (answer is not null) return await RecordAsync(question, answer);

        if (eligible && await checkpointWriter.TryCheckpointAsync(pipeline, question, jobId, cancellationToken))
        {
            pipeline.Set(ContextKeys.WaitingForInput, true);
            return DialogueAskOutcome.Parked();
        }

        logger.LogWarning("Question '{QuestionId}' timed out, using default: {Default}",
            question.QuestionId, question.DefaultAnswer ?? "");
        return await RecordAsync(question, DefaultAnswerFor(question, "timeout"));
    }

    // The resumed answer is consumed once and re-keyed to the CURRENT question id:
    // a handler that mints its question id per execution (Approval) produces a
    // fresh id on re-entry, but deterministic re-execution reaches the same ask.
    private static bool TryConsumeResumedAnswer(
        PipelineContext pipeline, DialogQuestion question, out DialogAnswer? answer)
    {
        if (pipeline.TryGet<DialogAnswer>(ContextKeys.ResumedDialogueAnswer, out var delivered)
            && delivered is not null)
        {
            pipeline.Remove(ContextKeys.ResumedDialogueAnswer);
            answer = delivered with { QuestionId = question.QuestionId };
            return true;
        }
        answer = null;
        return false;
    }

    private string? ResolveDialogueJobId(PipelineContext pipeline)
    {
        if (progressReporter.JobId is { Length: > 0 } jobId) return jobId;
        return pipeline.TryGet<string>(ContextKeys.RunId, out var runId)
            && !string.IsNullOrEmpty(runId) ? runId : null;
    }

    // Eligible = a ticket run whose question outlives the hot window. Non-ticket
    // runs (CLI scans, spec-dialog turns) keep the full in-memory wait.
    private bool IsCheckpointEligible(
        PipelineContext pipeline, DialogQuestion question, out TimeSpan hotWindow)
    {
        var hotWait = pipeline.TryGet<int>(ContextKeys.DialogueHotWaitSeconds, out var seconds) && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(DefaultHotWaitSeconds);
        var eligible = pipeline.Has(ContextKeys.TicketId) && question.Timeout > hotWait;
        hotWindow = eligible ? hotWait : question.Timeout;
        return eligible;
    }

    private static DialogAnswer DefaultAnswerFor(DialogQuestion question, string comment) => new(
        question.QuestionId, question.DefaultAnswer ?? "", comment, DateTimeOffset.UtcNow, "system");

    private async Task<DialogueAskOutcome> RecordAsync(DialogQuestion question, DialogAnswer answer)
    {
        await dialogueTrail.RecordAsync(question, answer);
        return DialogueAskOutcome.Answered(answer);
    }
}
