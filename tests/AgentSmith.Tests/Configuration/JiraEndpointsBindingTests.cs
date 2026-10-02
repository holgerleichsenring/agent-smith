using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// A tracker's Jira <c>endpoints:</c> block was documented and never bound: the raw entry had
/// no such key, so an operator's override was dropped and the defaults ran.
/// </summary>
public sealed class JiraEndpointsBindingTests
{
    private const string Yaml = """
        trackers:
          j:
            type: jira
            url: https://acme.atlassian.net/
            auth: jira_token
            endpoints:
              search: /rest/api/2/search
        """;

    [Fact]
    public void TrackerCatalog_EndpointsBlock_ReachesTheConnection()
    {
        var raw = new RawConfigYaml().Deserialize(Yaml);

        var tracker = new TrackerCatalogBuilder().Build(raw.Trackers, [], []).Single().Value;

        tracker.Endpoints.Search.Should().Be("/rest/api/2/search");
        tracker.Endpoints.Issue.Should().Be(new JiraEndpoints().Issue, "a key left out keeps its default");
    }

    [Fact]
    public void Studio_Endpoints_RoundTripOnlyTheOverrides()
    {
        var raw = new RawConfigYaml().Deserialize(Yaml);

        var entity = ConfigCatalogMapper.ToCatalog(raw).Trackers.Single();
        var patched = RawConfigPatch.Tracker(entity with { Endpoints = new Dictionary<string, string>
        {
            ["search"] = "/rest/api/2/search", ["create"] = "/rest/api/2/issue",
        } }, null);

        entity.Endpoints.Should().BeEquivalentTo(new Dictionary<string, string> { ["search"] = "/rest/api/2/search" });
        patched.Endpoints!.Create.Should().Be("/rest/api/2/issue");
    }

    [Fact]
    public void Studio_UnknownEndpointKey_IsRefused()
    {
        var entity = new TrackerEntity("j", "jira", "tok", Endpoints: new Dictionary<string, string> { ["issue_link"] = "/x" });

        var act = () => RawConfigPatch.Tracker(entity, null);

        act.Should().Throw<AgentSmith.Domain.Exceptions.ConfigurationException>().WithMessage("*issue_link*");
    }
}
