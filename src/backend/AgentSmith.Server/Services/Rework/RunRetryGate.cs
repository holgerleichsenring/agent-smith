using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-9a59n: what the runs say about a Retry, before the tracker is read. Only the ticket's LATEST
/// attempt is retried — a newer queued reservation is an attempt already starting, which the previous-attempt
/// reader cannot see. The run asked must also be the latest the rework worker reads, or the stored request
/// would hang on a run the worker never looks at. A hand-back parks waiting with no finish, so it is answered
/// before "not finished".
/// <para>2026-11-17-166ad: one end rule for both origins — any end but a success is retried: failed, cancelled and
/// shortfall. The button used to admit only an infrastructure end, so restarting a failed attempt took a reply in
/// the conversation. The origin only words the hand-back answer.</para>
/// <para>2026-11-20-9936a: the page's head and the worker's latest may differ by runs a person cancelled while queued —
/// never started, so no attempt on the page, but attempts for the reader (they keep a poll off the ticket). A Retry of
/// the head, or of such a run newer than it, is one Retry: it is admitted on the head's end and recorded on the run
/// the worker reads (<see cref="RetryCheck.RecordOn"/>), so the stored request is the one the worker sees.</para>
/// </summary>
public sealed class RunRetryGate(
    TicketRunsQuery runQuery, TicketAttempts attempts, IPreviousAttemptReader worker,
    Application.Services.Claim.UnmetTicketRequirements? requirements = null) // 2026-11-19-1b72a: is the series still paused
{
    /// <summary>The refusal, or the run the request is recorded on when the run may be retried.</summary>
    public async Task<RetryCheck> CheckAsync(
        string project, string ticketId, string runId, RetryOrigin origin, CancellationToken ct)
    {
        var code = (await runQuery.ForTicketAsync(project, ticketId, ct))
            .Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        RetryRefusalView Refuse(string reason, string text) => new(reason, text, project, ticketId);
        var head = attempts.Head(code).Run;
        var read = (await worker.LatestAsync(project, ticketId, null, ct))?.RunId;
        if (head.Id != runId && head.Status == RunStatuses.Queued)
            return Refuse(RetryRefusals.Starting, $"{RetryRefusals.StartingText} — attempt {head.Id} is queued");
        if (head.Id != runId && !(runId == read && NeverStartedAfter(code, head, read)))
            return Refuse(RetryRefusals.NotLatest, NotLatest(head.Id));
        if (head.EndKind == RunEndKinds.SeriesPaused) return await HeldDropAsync(project, ticketId, Refuse, ct);
        if (read is null || (read != head.Id && !NeverStartedAfter(code, head, read)))
            return Refuse(RetryRefusals.NotLatest, $"attempt {read ?? "none"} is the latest the rework worker reads — "
                + "only the latest attempt is retried");
        return EndRefusal(head, origin, Refuse) is { } refused ? refused : RetryCheck.Admit(read);
    }

    // 2026-11-20-9936a: the reader's latest is a code row newer than the head that the page counts no attempt — cancelled
    // before any step, a person's queued cancel (the reader drops the pump's own drops).
    private bool NeverStartedAfter(IReadOnlyList<TicketRunRow> code, Infrastructure.Persistence.Entities.Run head, string? read) =>
        read is not null && string.CompareOrdinal(read, head.Id) > 0
        && code.FirstOrDefault(r => r.Run.Id == read) is { } row && !attempts.IsAttempt(row);

    // The end of the head decides, whichever of the two the Retry was asked of.
    private static RetryRefusalView? EndRefusal(Infrastructure.Persistence.Entities.Run head, RetryOrigin origin,
        Func<string, string, RetryRefusalView> refuse)
    {
        if (head.Status == RunStatuses.WaitingForInput)
            return refuse(RetryRefusals.HandedBack, origin == RetryOrigin.Conversation
                ? RetryRefusals.HandedBackConversationText : RetryRefusals.HandedBackText);
        if (head.FinishedAt is null)
            return refuse(RetryRefusals.NotFinished, $"run {head.Id} has not finished — it is {head.Status}");
        return head.Status == RunStatuses.Success ? refuse(RetryRefusals.Delivered, RetryRefusals.DeliveredText) : null;
    }

    // 2026-11-19-1b72a: a start the pump let go while the series was paused is no attempt to retry. While the series is
    // still paused the pause is the hold; once it runs again its resume starts the ticket; otherwise a start by name does.
    private async Task<RetryCheck> HeldDropAsync(
        string project, string ticketId, Func<string, string, RetryRefusalView> refuse, CancellationToken ct)
    {
        if (requirements is not null && await requirements.PausedSeriesAsync(project, ticketId, ct) is not null)
            return refuse(RetryRefusals.SeriesPaused, Application.Services.Claim.RequiresUnmetGate.SeriesPaused);
        var running = requirements is not null && await requirements.RunningSeriesAsync(project, ticketId, ct) is not null;
        return refuse(RetryRefusals.SeriesPaused, running ? ResumedDropText : EndedDropText);
    }

    /// <summary>2026-11-19-1b72a: a paused-series drop whose series runs again.</summary>
    public const string ResumedDropText = "this start was let go while the ticket's series was paused and is no attempt; "
        + "the series' resume starts it";

    /// <summary>2026-11-19-1b72a: a paused-series drop whose series no longer runs.</summary>
    public const string EndedDropText = "this start was let go while the ticket's series was paused and is no attempt; "
        + "start it by name";

    private static string NotLatest(string headId) =>
        $"attempt {headId} is the ticket's latest — only the latest attempt is retried";
}

/// <summary>2026-11-20-9936a: the gate's answer — a refusal, or the run the admitted request is recorded on.</summary>
public sealed record RetryCheck(RetryRefusalView? Refusal, string? RecordOn)
{
    public static RetryCheck Admit(string recordOn) => new(null, recordOn);

    public static implicit operator RetryCheck(RetryRefusalView refusal) => new(refusal, null);
}
