using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0461: the ticket end of a parked run — the channel the operator actually uses.
/// <para>
/// p0453 made a mid-run question answerable in the dashboard: the checkpoint is consumed
/// and the SAME run continues. But the question also arrives as a work-item comment, in
/// their mail, and an answer written there reached nothing — the checkpoint sat unconsumed
/// until a hand-moved status started a FRESH run and discarded the parked one. Two channels
/// that look identical to the person writing in them, doing two different things.
/// </para>
/// <para>
/// Both halves live here because both are the same relationship: this run's ticket. One
/// reads an answer off it, the other tells it the run moved on.
/// </para>
/// </summary>
public interface IParkedTicketDialogue
{
    /// <summary>
    /// Reads the checkpoint's ticket for an operator reply written AFTER the question was
    /// asked and delivers it to the answer inbox. Returns true when one landed. Not a
    /// second resume path: it fills the same first-answer-wins inbox the dashboard fills,
    /// and the resume sweeper does the resuming.
    /// </summary>
    Task<bool> TryCollectAnswerAsync(RunCheckpointRecord checkpoint, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the ticket off its clarification status now that the run is resuming. Does
    /// nothing when the trigger declares no in_progress_status — and says so, because a
    /// silent no-op is how a board goes on reading "waiting for you" over a working run.
    /// </summary>
    Task MoveToInProgressAsync(RunCheckpointRecord checkpoint, CancellationToken cancellationToken);
}
