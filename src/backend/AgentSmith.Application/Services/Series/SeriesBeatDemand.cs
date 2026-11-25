using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: why a series needs a beat now, from its record and its tickets' facts; null when it
/// needs none. 2026-11-17-0207b: an operator message addressed to it is a reason, and so is the status cadence.
/// 2026-11-25-9992e: a delivering series with final pull requests is read every merge.ci_poll_seconds, so a final pull request
/// its master merged or armed ends the series at the next poll. That reason only lets the delivery read; it is no master
/// reason (ReadSeriesHandler never turns it into one), so the model is not called for it.</summary>
public sealed class SeriesBeatDemand(ISeriesConversation conversation, AgentSmithConfig config)
{
    public const string FinalsRead = "the final pull requests are read";

    public async Task<string?> WhyAsync(
        SeriesRunRecord record, IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (SeriesBeatDue.Why(record, facts, now, config.Dialogue.SeriesStatusMinutes) is { } why) return why;
        if (!record.IsActive) return null;
        if (FinalsDue(record, now)) return FinalsRead;
        var waiting = await conversation.AddressedAsync(record.Conversation, record.Id, record.MessagesThrough, ct);
        return waiting.Count > 0 ? "the operator wrote" : null;
    }

    // The last beat's clock: every beat that ran stamps LastStatusAt.
    private bool FinalsDue(SeriesRunRecord record, DateTimeOffset now) =>
        record is { State: SeriesRunStates.Delivering, Deliveries.Count: > 0 }
        && config.Projects.TryGetValue(record.Project, out var project)
        && (record.LastStatusAt is not { } last || now - last >= TimeSpan.FromSeconds(Math.Max(1, project.Merge.CiPollSeconds)));
}
