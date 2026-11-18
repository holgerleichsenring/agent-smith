using AgentSmith.Contracts.Series;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207a: the series store held in memory, for the step tests.</summary>
internal sealed class InMemorySeriesRunStore : ISeriesRunStore
{
    public Dictionary<string, SeriesRunRecord> Records { get; } = new(StringComparer.Ordinal);

    public Task OpenAsync(SeriesRunRecord record, CancellationToken cancellationToken)
    {
        Records[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<SeriesRunRecord?> GetAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(Records.GetValueOrDefault(id));

    public Task<SeriesRunRecord?> ActiveForConversationAsync(string conversation, CancellationToken cancellationToken) =>
        Task.FromResult(Records.Values.LastOrDefault(r => r.Conversation == conversation && r.IsActive));

    public Task<IReadOnlyList<SeriesRunRecord>> ActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SeriesRunRecord>>([.. Records.Values.Where(r => r.IsActive)]);

    public Task<SeriesRunRecord?> LatestForConversationAsync(string conversation, CancellationToken cancellationToken) =>
        Task.FromResult(Records.Values.LastOrDefault(r => r.Conversation == conversation));

    public Task<SeriesRunRecord?> UpdateAsync(string id, Func<SeriesRunRecord, SeriesRunRecord> change, CancellationToken cancellationToken)
    {
        if (!Records.TryGetValue(id, out var record)) return Task.FromResult<SeriesRunRecord?>(null);
        var changed = change(record) with { Version = record.Version + 1 };
        Records[id] = changed;
        return Task.FromResult<SeriesRunRecord?>(changed);
    }
}
