using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: what a series is called — its conversation's subject, else its first ticket's title, else its
/// id — for many series in one read.</summary>
public sealed class SeriesRunNames(SpecDialogSessionRepository sessions)
{
    public async Task<IReadOnlyDictionary<string, string>> OfAsync(IReadOnlyList<SeriesRunRecord> records, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(records);
        var subjects = (await sessions.ListBySessionIdsAsync([.. records.Select(r => r.Conversation).Distinct(StringComparer.Ordinal)], ct))
            .Where(s => !string.IsNullOrWhiteSpace(s.Subject))
            .GroupBy(s => s.SessionId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Subject!, StringComparer.Ordinal);
        return records.ToDictionary(r => r.Id, r => subjects.GetValueOrDefault(r.Conversation) ?? r.Tickets.FirstOrDefault()?.Title ?? r.Id,
            StringComparer.Ordinal);
    }
}
