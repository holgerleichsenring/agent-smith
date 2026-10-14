using System.Text.Json;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using FluentAssertions;

namespace AgentSmith.Tests.Spawning;

/// <summary>2026-10-13-8762v: the series' automatic start rides the run's initial context in both shapes the funnel
/// emits, so a start the capacity queue defers keeps its cause and its admission.</summary>
public sealed class SeriesAutoStartSeedTests
{
    private static readonly ResolvedProject Project = new() { Name = "sample", Repos = [] };

    private static IncomingTicketEnvelope Envelope(bool series) =>
        new() { TicketId = "42", Platform = "github", RequestedByName = true, SeriesAutoStart = series };

    [Fact]
    public void SpawnRequestBuilder_SeriesAutoStart_RidesTheQueueRow()
    {
        var request = SpawnRequestBuilder.BuildRequest(Project, "code", Envelope(series: true), new WebhookTriggerConfig(), null, "run-1");
        var candidate = SpawnRequestBuilder.BuildCandidate(
            Project, "code", Envelope(series: true), new WebhookTriggerConfig(), null, "run-2", "queued");
        var queued = JsonSerializer.Deserialize<Dictionary<string, object>>(candidate.InitialContextJson!);

        request.InitialContext.IsSeriesAutoStart().Should().BeTrue();
        queued.IsSeriesAutoStart().Should().BeTrue("the queue row round-trips it as a JSON true");
        queued.IsRequestedByName().Should().BeTrue();
        SpawnRequestBuilder.BuildRequest(Project, "code", Envelope(series: false), new WebhookTriggerConfig(), null, "run-3")
            .InitialContext!.ContainsKey(ContextKeys.SeriesAutoStart).Should().BeFalse("only a series start carries the key");
    }
}
