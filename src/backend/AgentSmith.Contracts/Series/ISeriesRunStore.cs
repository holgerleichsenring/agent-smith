namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207a: the series runs. An update re-reads and re-applies its change when another writer won,
/// so two writers never lose each other's change.</summary>
public interface ISeriesRunStore
{
    Task OpenAsync(SeriesRunRecord record, CancellationToken cancellationToken);

    Task<SeriesRunRecord?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>The conversation's running or verifying series, if it has one.</summary>
    Task<SeriesRunRecord?> ActiveForConversationAsync(string conversation, CancellationToken cancellationToken);

    /// <summary>Every running or verifying series.</summary>
    Task<IReadOnlyList<SeriesRunRecord>> ActiveAsync(CancellationToken cancellationToken);

    /// <summary>The newest series of a conversation, active or ended; null when it never had one.</summary>
    Task<SeriesRunRecord?> LatestForConversationAsync(string conversation, CancellationToken cancellationToken);

    /// <summary>Applies the change to the stored record and saves it; null when there is no such series.</summary>
    Task<SeriesRunRecord?> UpdateAsync(
        string id, Func<SeriesRunRecord, SeriesRunRecord> change, CancellationToken cancellationToken);
}
