using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Webhooks.Routing;

/// <summary>2026-10-08-e8b9a: the ticket-comment handlers no test named, on the payloads the
/// platforms document.</summary>
public sealed class TicketCommentHandlerTests
{
    private static string AzureCommented(string history) => $$"""
        { "eventType": "workitem.commented", "publisherId": "tfs",
          "resource": { "id": 5, "url": "https://dev.azure.com/o/_apis/wit/workItems/5",
            "fields": { "System.State": "New", "System.History": "{{history}}" } } }
        """;

    // Microsoft's 2018 sample: resource.id is the update number, the item is under revision.
    private const string AzureUpdateShape = """
        { "eventType": "workitem.updated", "publisherId": "tfs",
          "resource": { "id": 2, "workItemId": 0,
            "fields": { "System.State": { "oldValue": "New", "newValue": "Approved" } },
            "revision": { "id": 5, "fields": { "System.State": "New", "System.Tags": "x" } } } }
        """;

    private const string AzureFlatShape = """
        { "eventType": "workitem.updated", "publisherId": "tfs",
          "resource": { "id": 5, "fields": { "System.State": "New", "System.Tags": "x" } } }
        """;

    [Fact]
    public async Task AzureDevOpsComment_KeywordInHtmlHistory_Dispatches()
    {
        var f = new TicketCommentHandlerFixture(TrackerType.AzureDevOps);

        var result = await f.AzureDevOpsComment().HandleAsync(
            AzureCommented("<div>please @agent-smith <b>fix</b></div>"), TicketCommentHandlerFixture.NoHeaders);

        result.Handled.Should().BeTrue();
        f.VerifySpawned("5", Times.Once());
    }

    [Fact]
    public async Task AzureDevOpsComment_NoKeyword_NotHandled()
    {
        var f = new TicketCommentHandlerFixture(TrackerType.AzureDevOps);

        var result = await f.AzureDevOpsComment().HandleAsync(AzureCommented("just a note"), TicketCommentHandlerFixture.NoHeaders);

        result.Handled.Should().BeFalse();
        f.VerifySpawned("5", Times.Never());
    }

    [Fact]
    public async Task AzureDevOpsWorkItemUpdated_UpdateShape_UsesRevisionIdAndState()
    {
        var f = new TicketCommentHandlerFixture(TrackerType.AzureDevOps);

        var result = await f.AzureDevOpsUpdated().HandleAsync(AzureUpdateShape, TicketCommentHandlerFixture.NoHeaders);

        result.Handled.Should().BeTrue();
        f.VerifySpawned("5", Times.Once());
        f.VerifySpawned("2", Times.Never());
    }

    [Fact]
    public async Task AzureDevOpsWorkItemUpdated_FlatShape_StillDispatches()
    {
        var f = new TicketCommentHandlerFixture(TrackerType.AzureDevOps);

        await f.AzureDevOpsUpdated().HandleAsync(AzureFlatShape, TicketCommentHandlerFixture.NoHeaders);

        f.VerifySpawned("5", Times.Once());
    }

    [Fact]
    public async Task CommentHandlers_OurOwnComment_NotHandled()
    {
        var azure = new TicketCommentHandlerFixture(TrackerType.AzureDevOps);
        var github = new TicketCommentHandlerFixture(TrackerType.GitHub);
        var gitlab = new TicketCommentHandlerFixture(TrackerType.GitLab);
        const string ours = "Agent Smith — no agent-smith project matched this ticket. @agent-smith";

        (await azure.AzureDevOpsComment().HandleAsync(AzureCommented(ours), TicketCommentHandlerFixture.NoHeaders))
            .Handled.Should().BeFalse();
        (await github.GitHubComment().HandleAsync(GitHubComment(ours, pullRequest: false), TicketCommentHandlerFixture.NoHeaders))
            .Handled.Should().BeFalse();
        (await gitlab.GitLabComment().HandleAsync(GitLabNote(ours, "Issue"), TicketCommentHandlerFixture.NoHeaders))
            .Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitHubIssueComment_KeywordOnPullRequestIssue_NotHandled()
    {
        var f = new TicketCommentHandlerFixture(TrackerType.GitHub);

        var result = await f.GitHubComment().HandleAsync(GitHubComment("@agent-smith go", pullRequest: true), TicketCommentHandlerFixture.NoHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitHubIssueComment_KeywordOnIssue_Dispatches()
    {
        var f = new TicketCommentHandlerFixture(TrackerType.GitHub);

        await f.GitHubComment().HandleAsync(GitHubComment("@agent-smith go", pullRequest: false), TicketCommentHandlerFixture.NoHeaders);

        f.VerifySpawned("7", Times.Once());
    }

    [Fact]
    public async Task GitLabIssueComment_NoteOnMergeRequest_NotHandled()
    {
        var f = new TicketCommentHandlerFixture(TrackerType.GitLab);

        var result = await f.GitLabComment().HandleAsync(GitLabNote("@agent-smith go", "MergeRequest"), TicketCommentHandlerFixture.NoHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitLabIssueComment_KeywordOnIssue_Dispatches()
    {
        var f = new TicketCommentHandlerFixture(TrackerType.GitLab);

        await f.GitLabComment().HandleAsync(GitLabNote("@agent-smith go", "Issue"), TicketCommentHandlerFixture.NoHeaders);

        f.VerifySpawned("3", Times.Once());
    }

    private static string GitHubComment(string body, bool pullRequest) => $$"""
        { "action": "created",
          "issue": { "number": 7, "state": "open", "labels": [] {{(pullRequest ? ", \"pull_request\": {}" : "")}} },
          "comment": { "body": "{{body}}" },
          "repository": { "html_url": "https://github.com/o/r" } }
        """;

    private static string GitLabNote(string body, string noteableType) => $$"""
        { "object_kind": "note",
          "object_attributes": { "note": "{{body}}", "noteable_type": "{{noteableType}}" },
          "project": { "web_url": "https://gitlab.com/o/r" },
          "issue": { "iid": 3, "state": "opened" } }
        """;
}
