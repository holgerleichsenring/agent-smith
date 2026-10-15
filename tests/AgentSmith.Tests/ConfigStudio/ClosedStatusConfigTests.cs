using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-10-15-114fe: a tracker's closed_status — where "also close on the tracker" moves a ticket — travels from
/// agentsmith.yml to the connection and through the Config Studio, declared for Jira and Azure DevOps only.
/// </summary>
public sealed class ClosedStatusConfigTests
{
    private const string Yaml = """
        trackers:
          j:
            type: jira
            url: https://acme.atlassian.net/
            auth: jira_token
            email: agent@acme.example
            done_status: Done
            closed_status: Won't Do
        """;

    [Fact]
    public void TrackerCatalogBuilder_ClosedStatus_ReachesTheTrackerConnection()
    {
        var raw = new RawConfigYaml().Deserialize(Yaml);

        var tracker = new TrackerCatalogBuilder().Build(raw.Trackers, [], []).Single().Value;

        tracker.ClosedStatus.Should().Be("Won't Do");
        tracker.DoneStatus.Should().Be("Done");
    }

    [Fact]
    public void ConfigCatalogMapper_ClosedStatus_RoundTripsThroughTheStudioEntity()
    {
        var raw = new RawConfigYaml().Deserialize(Yaml);

        var entity = ConfigCatalogMapper.ToCatalog(raw).Trackers.Single();
        var patched = RawConfigPatch.Tracker(entity with { ClosedStatus = "Abandoned" }, raw.Trackers["j"]);

        entity.ClosedStatus.Should().Be("Won't Do");
        patched.ClosedStatus.Should().Be("Abandoned");
    }

    [Fact]
    public void Capabilities_ClosedStatus_IsDeclaredForJiraAndAzureDevOpsOnly()
    {
        var capabilities = ConfigStudioCapabilities.Build(["claude"]);

        capabilities.TrackerTypes.Where(t => t.Fields.Any(f => f.Key == "closedStatus")).Select(t => t.Type)
            .Should().BeEquivalentTo(["azure_devops", "jira"]);
        var field = capabilities.TrackerTypes.Single(t => t.Type == "jira").Fields.Single(f => f.Key == "closedStatus");
        field.Required.Should().BeFalse("Required is enforced on every upsert and would make every existing tracker unsaveable");
        field.Group.Should().Be(TrackerFieldGroups.Transition);
    }
}
