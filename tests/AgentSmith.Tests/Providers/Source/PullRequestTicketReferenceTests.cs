using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-10-25-9dbba: a pull request references its ticket in the host's own syntax only when
/// the ticket lives on that host, never with a closing keyword, and otherwise by its web link.
/// </summary>
public sealed class PullRequestTicketReferenceTests
{
    private const string GitHubRepoUrl = "https://github.com/example/sample-server.git";
    private const string AzureWorkItemUrl = "https://dev.azure.com/example/demo/_workitems/edit/4711";

    [Fact]
    public void GitHubSourceProvider_JiraTicket_FootsWebLinkNoKeyword()
    {
        var ticket = new PullRequestTicket(
            new TicketId("DEMO-12"), TrackerType.Jira, "https://example.atlassian.net/browse/DEMO-12");

        var body = GitHubTicketReference.Compose("body", ticket, GitHubRepoUrl, "example", "sample-server");

        body.Should().Be("body\n\nTicket: https://example.atlassian.net/browse/DEMO-12");
    }

    [Fact]
    public void GitHubSourceProvider_AzureDevOpsWorkItem_NeverWritesClosesHashId()
    {
        var ticket = new PullRequestTicket(new TicketId("4711"), TrackerType.AzureDevOps, AzureWorkItemUrl);

        var body = GitHubTicketReference.Compose("body", ticket, GitHubRepoUrl, "example", "sample-server");

        body.Should().NotContain("Closes").And.NotContain("#4711").And.EndWith($"Ticket: {AzureWorkItemUrl}");
    }

    [Fact]
    public void GitHubSourceProvider_SameRepoIssue_ReferencesHashNumberWithoutCloses()
    {
        var ticket = GitHubIssue("https://github.com/Example/Sample-Server/issues/12");

        var body = GitHubTicketReference.Compose("body", ticket, GitHubRepoUrl, "example", "sample-server");

        body.Should().Be("body\n\nTicket: #12");
    }

    [Fact]
    public void GitHubSourceProvider_OtherRepoIssue_ReferencesOwnerRepoNumber()
    {
        var ticket = GitHubIssue("https://github.com/example/planning/issues/12");

        var body = GitHubTicketReference.Compose("body", ticket, GitHubRepoUrl, "example", "sample-server");

        body.Should().Be("body\n\nTicket: example/planning#12");
    }

    [Fact]
    public void GitHubSourceProvider_IssueOnAnotherHost_FootsWebLink()
    {
        var ticket = GitHubIssue("https://github.example.com/example/planning/issues/12");

        var body = GitHubTicketReference.Compose("body", ticket, GitHubRepoUrl, "example", "sample-server");

        body.Should().Be("body\n\nTicket: https://github.example.com/example/planning/issues/12");
    }

    [Fact]
    public void GitHubSourceProvider_TicketWithoutWebUrl_AddsNothing()
    {
        var ticket = PullRequestTicket.Of(new TicketId("12"), TrackerType.GitHub);

        GitHubTicketReference.Compose("body", ticket, GitHubRepoUrl, "example", "sample-server")
            .Should().Be("body");
    }

    [Fact]
    public void GitLabTicketReference_SameProjectIssue_ReferencesHashNumber() =>
        GitLabTicketReference.Compose("body", GitLabIssue("https://gitlab.example.com/group/repo/-/issues/5"),
            "https://gitlab.example.com", "group/repo").Should().Be("body\n\nTicket: #5");

    [Fact]
    public void GitLabTicketReference_OtherProjectIssue_ReferencesPathNumber() =>
        GitLabTicketReference.Compose("body", GitLabIssue("https://gitlab.example.com/group/sub/plan/-/issues/5"),
            "https://gitlab.example.com", "group/repo").Should().Be("body\n\nTicket: group/sub/plan#5");

    [Fact]
    public void AzureReposSourceProvider_GitHubIssue_LinksNoWorkItem() =>
        AzureDevOpsWorkItemLink.WorkItemIdFor(
            GitHubIssue("https://github.com/example/planning/issues/12"), "https://dev.azure.com/example")
            .Should().BeNull("a GitHub issue #12 is not Azure DevOps work item 12");

    [Theory]
    [InlineData("https://dev.azure.com/example", 4711)]
    [InlineData("https://example.visualstudio.com", 4711)]
    [InlineData("https://dev.azure.com/other", null)]
    public void AzureDevOpsWorkItemLink_LinksOnlyItsOwnOrganization(string organizationUrl, int? expected)
    {
        var ticket = new PullRequestTicket(new TicketId("4711"), TrackerType.AzureDevOps, AzureWorkItemUrl);

        AzureDevOpsWorkItemLink.WorkItemIdFor(ticket, organizationUrl).Should().Be(expected);
    }

    [Fact]
    public void AzureDevOpsWorkItemLink_InitRunWithoutWebUrl_TakesTheTrackerAtItsWord() =>
        AzureDevOpsWorkItemLink.WorkItemIdFor(
            PullRequestTicket.Of(new TicketId("4711"), TrackerType.AzureDevOps), "https://dev.azure.com/example")
            .Should().Be(4711);

    [Fact]
    public void TicketReferenceFooter_AppendWebLink_NoTicket_LeavesTheDescription() =>
        TicketReferenceFooter.AppendWebLink("body", null).Should().Be("body");

    private static PullRequestTicket GitHubIssue(string url) => new(new TicketId("12"), TrackerType.GitHub, url);

    private static PullRequestTicket GitLabIssue(string url) => new(new TicketId("5"), TrackerType.GitLab, url);
}
