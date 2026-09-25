using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Polling;

/// <summary>
/// 2026-09-25-c1f7: the guard extracted out of <see cref="TrackerDiscoveryQueryBuilder"/> when
/// that file reached its length ceiling. Its two rules are unchanged and asserted here directly.
/// </summary>
public sealed class DiscoveryLabelGuardTests
{
    [Fact]
    public void For_ALabelGatedProject_NamesItsKeysAndEveryPhaseExecutionBinding()
    {
        var guard = DiscoveryLabelGuard.For([Trigger("bug")], Tracker());

        guard.Should().BeEquivalentTo(
            ["bug", FiledTicketLabels.ApprovedSetStamp, PhaseTicketRenderer.PhaseLabel]);
    }

    [Fact]
    public void For_NoProjectFiltersByLabel_IsEmpty()
    {
        DiscoveryLabelGuard.For([Trigger()], Tracker()).Should().BeEmpty(
            "a guard naming only the framework's keys would hide every ordinary ticket");
    }

    private static TrackerConnection Tracker() =>
        new() { Name = "jira-main", Type = TrackerType.Jira };

    private static WebhookTriggerConfig Trigger(string? labelKey = null) =>
        new()
        {
            TriggerStatuses = ["To Do"],
            PipelineFromLabel = labelKey is null
                ? null
                : new Dictionary<string, string> { [labelKey] = "fix-bug" },
        };
}
