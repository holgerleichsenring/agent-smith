using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-27-3532a: the wake that tells a series master a ticket of its series waits for start_ticket — recorded when a tracker
/// triggers the ticket and when a filing opens or joins the series with it, so a webhook that sends no event still reaches the
/// master. It is a Prerequisite-kind wake raised once per series, ticket and latest attempt (<see cref="OnceKey"/>): a re-poll, a
/// redelivered webhook and the filing's own record of it wake the master once, and a trigger after a new attempt wakes it again.
/// </summary>
public static class SeriesStartWakes
{
    public const string Filed = "it was filed into the series; start_ticket starts it once what it requires is met";

    /// <summary>The attempt part of a ticket no run worked yet.</summary>
    public const string NoAttempt = "none";

    /// <param name="attempt">The ticket's latest attempt's run id, or <see cref="NoAttempt"/>.</param>
    public static string OnceKey(string seriesId, string ticketId, string attempt = NoAttempt) => $"start:{seriesId}:{ticketId}:{attempt}";

    public static SeriesWake For(string seriesId, string ticketId, string text, string attempt = NoAttempt) =>
        new(SeriesWakeKinds.Prerequisite, text) { Once = OnceKey(seriesId, ticketId, attempt) };

    /// <summary>The record with the start wake of each ticket it has not raised yet, stamped at <paramref name="now"/>.</summary>
    public static SeriesRunRecord Recorded(SeriesRunRecord record, IEnumerable<string> ticketIds, string text, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        var fresh = ticketIds.Distinct(StringComparer.Ordinal)
            .Where(id => !record.Raised.Contains(OnceKey(record.Id, id), StringComparer.Ordinal)).ToList();
        return fresh.Count == 0 ? record : record with
        {
            Wakes = [.. record.Wakes, .. fresh.Select(id => For(record.Id, id, text) with { At = now, TicketId = id })],
            Raised = [.. record.Raised, .. fresh.Select(id => OnceKey(record.Id, id))],
        };
    }
}
