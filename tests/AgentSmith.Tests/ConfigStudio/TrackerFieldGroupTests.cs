using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-10-06-cea8: the studio places a tracker field by its group, so a field without one
/// lands on the first tab instead of where it belongs; and a draft check names each empty
/// required field on its own.
/// </summary>
public sealed class TrackerFieldGroupTests
{
    private static readonly string[] Groups =
    [
        TrackerFieldGroups.Connection, TrackerFieldGroups.Intake, TrackerFieldGroups.Outcome,
        TrackerFieldGroups.Transition, TrackerFieldGroups.Routing, TrackerFieldGroups.Filing,
    ];

    [Fact]
    public void TrackerCapabilityFields_EveryField_HasAGroup()
    {
        var fields = Enum.GetValues<TrackerType>().SelectMany(TrackerCapabilityFields.For);

        fields.Should().OnlyContain(f => Groups.Contains(f.Group));
    }

    [Fact]
    public void MissingTrackerFields_JiraWithoutEmail_ReturnsEmail()
    {
        var tracker = new TrackerEntity(
            Id: "j", Type: "jira", Url: "https://x.atlassian.net", AuthSecret: "JIRA_TOKEN");

        ConfigStudioCapabilities.MissingTrackerFields(tracker)
            .Select(f => f.Key).Should().Equal("email");
    }

    [Fact]
    public void MissingTrackerFields_UnknownType_Throws()
    {
        var tracker = new TrackerEntity(Id: "b", Type: "bugzilla", AuthSecret: null);

        FluentActions.Invoking(() => ConfigStudioCapabilities.MissingTrackerFields(tracker))
            .Should().Throw<ConfigurationException>().WithMessage("*unknown type*bugzilla*");
    }
}
