namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207a: the Work it out conversation a series reports into and, from 0207b, reads from.</summary>
public interface ISeriesConversation
{
    /// <summary>Keeps an agent message in the conversation and pushes it to the page; silent when it is closed.</summary>
    Task SayAsync(string conversation, string text, CancellationToken cancellationToken);

    /// <summary>2026-11-17-0207b: the operator messages addressed to the series after the given transcript index.</summary>
    Task<IReadOnlyList<SeriesOperatorMessage>> AddressedAsync(
        string conversation, string seriesRunId, int afterIndex, CancellationToken cancellationToken);

    /// <summary>2026-11-19-1b72a: whether the conversation is held on the dashboard, whose Series pane shows every ticket —
    /// there the series writes only what needs the operator; a chat platform has no pane and keeps its status table.</summary>
    Task<bool> HasPaneAsync(string conversation, CancellationToken cancellationToken);
}
