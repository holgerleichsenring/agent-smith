using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-10-02-5f89a: the Jira account email is a field of the tracker, required like the url it
/// belongs with; the project key stays optional (an unset one transitions under 'default').
/// </summary>
public sealed class CapabilityJiraEmailTests
{
    [Fact]
    public void Capabilities_JiraTracker_DeclaresARequiredEmailAndAnOptionalProject()
    {
        var fields = TrackerCapabilityFields.For(TrackerType.Jira);

        fields.Should().ContainSingle(f => f.Key == "email").Which.Required.Should().BeTrue();
        fields.Should().ContainSingle(f => f.Key == "project").Which.Required.Should().BeFalse();
    }

    [Fact]
    public void ValidateTracker_JiraWithoutEmail_IsRefusedNamingEmail()
    {
        var tracker = new TrackerEntity("j", "jira", "jira_token", Url: "https://jira.example");

        var act = () => ConfigStudioCapabilities.ValidateTracker(tracker);

        act.Should().Throw<ConfigurationException>().WithMessage("*email*");
    }
}
