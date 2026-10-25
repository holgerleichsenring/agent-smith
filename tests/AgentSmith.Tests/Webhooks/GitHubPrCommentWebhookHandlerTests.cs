using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitHubPrCommentWebhookHandlerTests
{
    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly PrCommentHandlerFixture _fixture = new();
    // 2026-10-25-9dbbe: GitHub is asked for the author's permission; writers by default.
    private string[] _permission = ["write"];

    private GitHubPrCommentWebhookHandler CreateSut()
    {
        var permissions = new Mock<IGitHubCollaboratorPermissionReader>();
        permissions.Setup(p => p.ReadAsync(It.IsAny<RepoConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _permission);
        var trust = new GitHubCollaboratorPermissionTrust(PrCommentHandlerFixture.Repos("https://github.com/org/my-api"),
            permissions.Object, PrCommentHandlerFixture.NewCache(), NullLogger<GitHubCollaboratorPermissionTrust>.Instance);
        return new(_fixture.Admission(), trust, _fixture.Heads(), NullLogger<GitHubPrCommentWebhookHandler>.Instance);
    }

    private static string IssueComment(
        string body, string association = "MEMBER", string action = "created", bool onPr = true) => $$"""
        {
            "action": "{{action}}",
            "comment": {
                "id": 100, "body": "{{body}}",
                "user": { "login": "dev-user" }, "author_association": "{{association}}"
            },
            "issue": { "number": 42 {{(onPr ? ", \"pull_request\": { \"url\": \"x\" }" : "")}} },
            "repository": { "full_name": "org/my-api", "html_url": "https://github.com/org/my-api" }
        }
        """;

    [Fact]
    public void CanHandle_CorrectEventTypes()
    {
        var sut = CreateSut();

        sut.CanHandle("github", "issue_comment").Should().BeTrue();
        sut.CanHandle("github", "pull_request_review_comment").Should().BeTrue();
        sut.CanHandle("github", "issues").Should().BeFalse();
        sut.CanHandle("gitlab", "issue_comment").Should().BeFalse();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("maintain")]
    [InlineData("write")]
    public async Task GitHubPrComment_FromWriter_StartsPipeline(string permission)
    {
        _permission = [permission];
        var result = await CreateSut().HandleAsync(
            IssueComment("/agent-smith fix #99 in my-api"), EmptyHeaders);

        result.Handled.Should().BeTrue();
        _fixture.LaunchedAs().Should().Be("code #99 pr:org/my-api#42");
    }

    // The payload's author_association no longer decides: a read-only COLLABORATOR is not a writer.
    [Theory]
    [InlineData("read")]
    [InlineData("triage")]
    [InlineData("none")]
    public async Task GitHubPrComment_WithoutWritePermission_IsNotHandled(string permission)
    {
        _permission = [permission];
        var result = await CreateSut().HandleAsync(IssueComment("/agent-smith fix", "COLLABORATOR"), EmptyHeaders);

        result.Handled.Should().BeFalse();
        _fixture.VerifyModelNeverAsked();
    }

    [Fact]
    public async Task GitHubPrComment_ReviewComment_FromCollaborator_StartsPipeline()
    {
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 101, "body": "/as security review",
                "user": { "login": "reviewer" }, "author_association": "COLLABORATOR"
            },
            "pull_request": { "number": 7 },
            "repository": { "full_name": "org/my-api", "html_url": "https://github.com/org/my-api" }
        }
        """;

        var result = await CreateSut().HandleAsync(payload, EmptyHeaders);
        _fixture.LaunchedAs().Should().Be("security-scan pr:org/my-api#7");
    }

    [Theory]
    [InlineData("Just a regular comment")]
    [InlineData("/agent-smith help")]
    [InlineData("/approve looks good")]
    public async Task GitHubPrComment_WithoutACommand_IsNotHandled(string body)
    {
        var result = await CreateSut().HandleAsync(IssueComment(body), EmptyHeaders);

        result.Handled.Should().BeFalse();
        _fixture.VerifyModelNeverAsked();
    }

    [Fact]
    public async Task GitHubPrComment_OnAPlainIssue_IsNotHandled()
    {
        var result = await CreateSut().HandleAsync(IssueComment("/agent-smith fix", onPr: false), EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitHubPrComment_Edited_IsNotHandled()
    {
        var result = await CreateSut().HandleAsync(IssueComment("/agent-smith fix", action: "edited"), EmptyHeaders);

        result.Handled.Should().BeFalse();
    }
}
