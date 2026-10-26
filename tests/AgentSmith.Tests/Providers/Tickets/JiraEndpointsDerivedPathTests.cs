using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-10-26-cb25c: the paths derived from the configured Jira endpoints, as the draft check and the issue link
/// computed them before ("was") and as JiraEndpoints computes them now.
/// </summary>
public sealed class JiraEndpointsDerivedPathTests
{
    [Theory]
    [InlineData(null, "/rest/api/3/project/OPS", "the check hard-coded /rest/api/3 — same")]
    [InlineData("/rest/api/2/myself", "/rest/api/2/project/OPS", "the check hard-coded /rest/api/3 — a v2 Data Center failed the Studio check")]
    [InlineData("/rest/api/2/myself/", "/rest/api/2/project/OPS", "the check hard-coded /rest/api/3")]
    [InlineData("/jira/rest/api/2/myself", "/jira/rest/api/2/project/OPS", "the check hard-coded /rest/api/3")]
    [InlineData("/rest/api/2", "/rest/api/2/project/OPS", "the check hard-coded /rest/api/3; a Myself without /myself is the root")]
    public void ProjectFor_FollowsTheMyselfApi(string? myself, string expected, string was)
    {
        var endpoints = myself is null ? new JiraEndpoints() : new JiraEndpoints { Myself = myself };

        endpoints.ProjectFor("OPS").Should().Be(expected, was);
    }

    [Fact]
    public void ProjectFor_EscapesTheKey() =>
        new JiraEndpoints().ProjectFor("A B").Should().Be("/rest/api/3/project/A%20B");

    [Theory]
    [InlineData(null, "/rest/api/3/issueLink")]
    [InlineData("/rest/api/2/issue", "/rest/api/2/issueLink")]
    [InlineData("/rest/api/3/issue/", "/rest/api/3/issueLink")]
    [InlineData("/custom/create", "/custom/createLink")]
    public void IssueLinkPath_IsTheIssueCollectionPlusLink_AsJiraRequiresLinkDerivedIt(string? create, string expected)
    {
        var endpoints = create is null ? new JiraEndpoints() : new JiraEndpoints { Create = create };

        endpoints.IssueLinkPath().Should().Be(expected, "unchanged: JiraRequiresLink appended Link to the trimmed Create path");
    }
}
