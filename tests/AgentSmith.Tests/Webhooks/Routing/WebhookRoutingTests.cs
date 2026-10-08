using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;

namespace AgentSmith.Tests.Webhooks.Routing;

/// <summary>2026-10-08-e8b9a: a recorded delivery goes through Detect and reaches the one
/// handler whose CanHandle accepts it — the step the handler tests used to skip.</summary>
public sealed class WebhookRoutingTests
{
    private const string AzureCommented = """
        { "eventType": "workitem.commented", "publisherId": "tfs",
          "resource": { "id": 5, "fields": { "System.State": "New", "System.History": "x" } } }
        """;

    private static Dictionary<string, string> Header(string name, string value) =>
        new(StringComparer.OrdinalIgnoreCase) { [name] = value };

    private static IWebhookHandler[] Handlers()
    {
        var f = new TicketCommentHandlerFixture(TrackerType.GitLab);
        return [f.GitLabIssue(), f.GitLabComment(), f.AzureDevOpsComment(), f.AzureDevOpsUpdated(), f.GitHubComment()];
    }

    private static IWebhookHandler Accepting(string path, string body, IDictionary<string, string> headers)
    {
        var (platform, eventType) = WebhookPlatformDetector.Detect(path, body, headers);
        return Handlers().Should().ContainSingle(h => h.CanHandle(platform!, eventType!)).Subject;
    }

    [Fact]
    public void Detect_AzureDevOpsBodyOnRootPath_ReturnsAzureDevOps()
    {
        var (platform, eventType) = WebhookPlatformDetector.Detect("/webhook", AzureCommented, TicketCommentHandlerFixture.NoHeaders);

        platform.Should().Be("azuredevops");
        eventType.Should().Be("workitem.commented");
    }

    [Fact]
    public void Detect_AzureDevOpsBodyOnGitHubPath_ReturnsNoPlatform()
    {
        WebhookPlatformDetector.Detect("/webhook/github", AzureCommented, TicketCommentHandlerFixture.NoHeaders)
            .Platform.Should().BeNull();
    }

    [Theory]
    [InlineData("""{ "eventType": "workitem.commented" }""")]
    [InlineData("""{ "eventType": "x", "publisherId": "someone-else" }""")]
    [InlineData("not json")]
    public void Detect_BodyWithoutDocumentedPublisher_ReturnsNoPlatform(string body)
    {
        WebhookPlatformDetector.Detect("/webhook", body, TicketCommentHandlerFixture.NoHeaders)
            .Platform.Should().BeNull();
    }

    [Fact]
    public void Routing_GitLabNoteHookOnIssue_ReachesIssueCommentHandler() =>
        Accepting("/webhook/gitlab", "{}", Header("X-Gitlab-Event", "Note Hook"))
            .Should().BeOfType<GitLabIssueCommentWebhookHandler>();

    [Fact]
    public void Routing_GitLabIssueHook_ReachesIssueHandler() =>
        Accepting("/webhook/gitlab", "{}", Header("X-Gitlab-Event", "Issue Hook"))
            .Should().BeOfType<GitLabIssueWebhookHandler>();

    [Fact]
    public void Routing_AzureDevOpsWorkItemCommented_ReachesCommentHandler() =>
        Accepting("/webhook", AzureCommented, TicketCommentHandlerFixture.NoHeaders)
            .Should().BeOfType<AzureDevOpsWorkItemCommentWebhookHandler>();
}
