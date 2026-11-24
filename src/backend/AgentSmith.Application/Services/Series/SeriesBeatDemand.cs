using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: why a series needs a beat now, from its record and its tickets' facts; null when it
/// needs none. 2026-11-17-0207b: an operator message addressed to it is a reason, and so is the status cadence.</summary>
public sealed class SeriesBeatDemand(ISeriesConversation conversation, AgentSmithConfig config)
{
    public async Task<string?> WhyAsync(
        SeriesRunRecord record, IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (SeriesBeatDue.Why(record, facts, now, config.Dialogue.SeriesStatusMinutes, DeliverEvery(record)) is { } why) return why;
        if (!record.IsActive) return null;
        var waiting = await conversation.AddressedAsync(record.Conversation, record.Id, record.MessagesThrough, ct);
        return waiting.Count > 0 ? "the operator wrote" : null;
    }

    // 2026-11-24-f9c5e: a series merging its final pull requests on green CI reads them at the CI poll.
    private TimeSpan? DeliverEvery(SeriesRunRecord record) =>
        config.Projects.TryGetValue(record.Project, out var project) && project.Merge.When == MergeWhen.OnCiGreen
            ? TimeSpan.FromSeconds(Math.Max(1, project.Merge.CiPollSeconds)) : null;
}
