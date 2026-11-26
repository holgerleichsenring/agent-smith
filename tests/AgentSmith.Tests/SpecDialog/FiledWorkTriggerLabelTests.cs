using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-11-26-5f6e: a ticket filed from a text gets the one pipeline_from_label key that routes to the code
/// pipeline — on every tracker — and never when it carries the approved-set stamp or a key the map resolves already.</summary>
public sealed class FiledWorkTriggerLabelTests
{
    private static readonly Dictionary<string, string> Map = new() { ["agent-smith:init"] = "init-project", ["agent-smith:code"] = "code" };

    private static ResolvedProject Project(TrackerType type, Dictionary<string, string>? map = null)
    {
        var tracker = new TrackerConnection { Name = "t", Type = type };
        return type switch
        {
            TrackerType.GitHub => new() { Name = "p", Tracker = tracker, GithubTrigger = new() { PipelineFromLabel = map ?? Map } },
            TrackerType.GitLab => new() { Name = "p", Tracker = tracker, GitlabTrigger = new() { PipelineFromLabel = map ?? Map } },
            TrackerType.AzureDevOps => new() { Name = "p", Tracker = tracker, AzuredevopsTrigger = new() { PipelineFromLabel = map ?? Map } },
            _ => new() { Name = "p", Tracker = tracker, JiraTrigger = new() { PipelineFromLabel = map ?? Map } },
        };
    }

    [Theory]
    [InlineData(TrackerType.GitHub)]
    [InlineData(TrackerType.GitLab)]
    [InlineData(TrackerType.AzureDevOps)]
    [InlineData(TrackerType.Jira)]
    public void For_ATicketWithOnlyItsRoutingTag_IsTheMapsCodeKey(TrackerType type) =>
        FiledWorkTriggerLabel.For(Project(type), ["NotificationCenter"]).Should().Be("agent-smith:code");

    [Theory]
    [InlineData(TrackerType.GitHub)]
    [InlineData(TrackerType.AzureDevOps)]
    public void For_AStampedOrAlreadyRoutedTicket_OrAMapWithoutCode_IsNothing(TrackerType type)
    {
        var stamp = TicketLabelVocabulary.For(new TrackerConnection { Name = "t", Type = type }).ApprovedSetStamp;

        FiledWorkTriggerLabel.For(Project(type), ["NotificationCenter", stamp]).Should().BeNull();
        FiledWorkTriggerLabel.For(Project(type), ["agent-smith:init"]).Should().BeNull();
        FiledWorkTriggerLabel.For(Project(type, new() { ["security"] = "security-scan" }), []).Should().BeNull();
        FiledWorkTriggerLabel.For(Project(type, []), []).Should().BeNull();
    }
}
