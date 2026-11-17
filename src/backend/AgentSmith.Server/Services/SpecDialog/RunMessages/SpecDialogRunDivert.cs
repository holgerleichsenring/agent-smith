using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog.RunMessages;

/// <summary>
/// 2026-11-17-4860b: a message in a ticket's conversation while a code attempt works the ticket is for that attempt.
/// No conversation turn runs: the stored message is stamped with the run's id — the run reads it with its master's next
/// tool result (<see cref="TicketConversationMessages"/>) — and the conversation says where it went. Only an ordinary
/// message of a poster who may start runs goes there: a card decision keeps its path, a cardless approve or reject word
/// is taken back first, and a poster without runs.control does not steer a running attempt.
/// </summary>
public sealed class SpecDialogRunDivert(
    SpecDialogTicketTextRepository ticketText, TicketRunningAttempt running, SpecDialogAnswerAdmission admission,
    SpecDialogSessionRepository repository, BoundSessionNudge bound, SpecDialogNotices notices,
    ILogger<SpecDialogRunDivert> logger)
{
    /// <summary>True when the message went to a running attempt and no turn is to run for it.</summary>
    public async Task<bool> DivertAsync(SpecDialogAdmitted admitted, TurnPoster poster, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(admitted);
        ArgumentNullException.ThrowIfNull(poster);
        var state = admitted.State;
        if (!poster.MayStartRuns || state.TicketKey is null || state.ThreadId is null) return false;
        if (admitted.Decision is not null && state.Transcript[^1].DecisionProposalId is not null) return false;
        if (await ticketText.GetAsync(state.JobId, ct) is not { TicketId: { Length: > 0 } ticketId }) return false;
        if (await running.RunIdAsync(state.Scope?.Project ?? state.Project, ticketId, ct) is not { } runId) return false;
        if (admitted.Decision is not null) state = await admission.UndecideAsync(state, ct);
        if (!await AddressAsync(state, state.Transcript[^1], runId, ct)) return false;
        logger.LogInformation("Conversation {Session}: a message for ticket {Ticket} goes to its running attempt {Run}",
            state.JobId, ticketId, runId);
        await notices.TellAsync(state, new ComposedReply(m =>
            $"Run {m.Bold(runId)} is working this ticket; your message reaches its agent with its next tool result."), ct, runId);
        return true;
    }

    // The stored message — the latest turn equal to it, so one stored after it is not touched — carries its run.
    private async Task<bool> AddressAsync(ConversationState state, TranscriptTurn stored, string runId, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(state.Platform, state.ThreadId!, ct);
        if (session is null) return false;
        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson).ToList();
        var index = transcript.LastIndexOf(stored);
        if (index < 0) return false;
        transcript[index] = transcript[index] with { RunId = runId };
        session.TranscriptJson = SpecDialogSessionMapper.WriteTranscript(transcript);
        await bound.SaveAsync(session, ct);
        return true;
    }
}
