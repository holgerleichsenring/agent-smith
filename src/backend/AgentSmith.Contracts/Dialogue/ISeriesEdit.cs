namespace AgentSmith.Contracts.Dialogue;

/// <summary>
/// Changing the bound ticket's series for its next attempt, as the person who posted the turn. Recording an
/// edit starts nothing. Built only for a worked ticket and a poster who holds runs.control.
/// </summary>
public interface ISeriesEdit
{
    /// <summary>The sentence the conversation is told: every spec's disposition, or why nothing was recorded.</summary>
    Task<string> EditAsync(IReadOnlyList<SeriesEdit> edits, CancellationToken cancellationToken);
}
