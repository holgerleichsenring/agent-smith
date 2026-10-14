using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-10-13-9a59j: who a parked segment waited on, read from what is already recorded — never stored again.
/// A question park is the question entry inside the segment, answered by the inbox row under the run id and
/// the question id, resumed when the next segment started. A hand-back park is the hand-back entry inside the
/// segment, answered by the cause of the ticket's next attempt that fetched its ticket (a reservation not yet
/// fetched has no cause). A park with neither entry inside it reads kind null — never guessed.
/// </summary>
public sealed class RunParkReader
{
    public async Task<RunComputeParkView?[]> ReadAsync(
        IUnitOfWork uow, string runId, IReadOnlyList<RunSegment> segments, CancellationToken ct)
    {
        var parks = new RunComputeParkView?[segments.Count];
        if (!segments.Any(s => s.EndStatus == RunStatuses.WaitingForInput)) return parks;
        var entries = await uow.Set<RunConversationEntry>().AsNoTracking()
            .Where(e => e.RunId == runId && e.EventSeq != null
                && (e.Kind == RunConversationKinds.Question || e.Kind == RunConversationKinds.HandBack))
            .OrderBy(e => e.EventSeq).ToListAsync(ct);
        var answers = await uow.Set<DialogueAnswerEntry>().AsNoTracking()
            .Where(a => a.DialogueJobId == runId).ToListAsync(ct);
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].EndStatus != RunStatuses.WaitingForInput) continue;
            var inside = entries.Where(e => RunSegmentsReader.Inside(segments, i, e.EventSeq)).ToList();
            var resumedAt = i + 1 < segments.Count ? segments[i + 1].StartedAt : (DateTimeOffset?)null;
            parks[i] = inside.LastOrDefault(e => e.Kind == RunConversationKinds.HandBack) is { } handback
                ? await HandbackAsync(uow, runId, handback, ct)
                : inside.LastOrDefault(e => e.Kind == RunConversationKinds.Question) is { } question
                    ? Question(question, answers.FirstOrDefault(a => a.QuestionId == question.QuestionId), resumedAt)
                    : new RunComputeParkView(null, null, null, null, null, resumedAt);
        }
        return parks;
    }

    private static RunComputeParkView Question(RunConversationEntry asked, DialogueAnswerEntry? answer, DateTimeOffset? resumedAt) =>
        new(RunParkKinds.Question, asked.At, asked.QuestionId, answer?.AnsweredAt, answer?.AnsweredBy, resumedAt);

    // The run that handed back never resumes; what ends its wait is the act that started the next attempt.
    private static async Task<RunComputeParkView> HandbackAsync(
        IUnitOfWork uow, string runId, RunConversationEntry asked, CancellationToken ct)
    {
        var ticket = await uow.Set<Run>().AsNoTracking().Where(r => r.Id == runId)
            .Select(r => new { r.Project, r.TicketId }).FirstOrDefaultAsync(ct);
        var next = ticket is null || ticket.TicketId.Length == 0 ? null : await uow.Set<Run>().AsNoTracking()
            .Where(r => r.Project == ticket.Project && r.TicketId == ticket.TicketId
                && r.Pipeline == PipelinePresets.CodeName && string.Compare(r.Id, runId) > 0)
            .Join(uow.Set<RunAttempt>(), r => r.Id, a => a.RunId, (r, a) => new { r.Id, a.CauseAt, a.CauseActor })
            .OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        return new RunComputeParkView(RunParkKinds.Handback, asked.At, null, next?.CauseAt, next?.CauseActor, null);
    }
}

/// <summary>2026-10-13-9a59j: what a park waited on.</summary>
public static class RunParkKinds
{
    public const string Question = "question";
    public const string Handback = "handback";
}

/// <summary>
/// 2026-10-13-9a59j: a segment that ended waiting for a person. Kind is null when nothing recorded what it
/// waited on; then AskedAt and the answer are null too. ResumedAt is the next launch's start (a hand-back is
/// never resumed — the ticket comes back as a new attempt).
/// </summary>
public sealed record RunComputeParkView(
    string? Kind, DateTimeOffset? AskedAt, string? QuestionId, DateTimeOffset? AnsweredAt, string? AnsweredBy,
    DateTimeOffset? ResumedAt);
