using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitHubPrCommentWebhookHandlerTests
{
    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly PrCommentHandlerFixture _fixture = new();

    private GitHubPrCommentWebhookHandler CreateSut() =>
        new(_fixture.Admission(), new GitHubAuthorAssociationTrust(),
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);

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
    [InlineData("OWNER")]
    [InlineData("MEMBER")]
    [InlineData("COLLABORATOR")]
    public async Task GitHubPrComment_FromWriter_StartsPipeline(string association)
    {
        var result = await CreateSut().HandleAsync(
            IssueComment("/agent-smith fix #99 in my-api", association), EmptyHeaders);

        result.Handled.Should().BeTrue();
        _fixture.LaunchedAs().Should().Be("code #99 pr:org/my-api#42");
    }

    [Theory]
    [InlineData("CONTRIBUTOR")]
    [InlineData("FIRST_TIME_CONTRIBUTOR")]
    [InlineData("NONE")]
    public async Task GitHubPrComment_ContributorAssociation_IsNotHandled(string association)
    {
        var result = await CreateSut().HandleAsync(IssueComment("/agent-smith fix", association), EmptyHeaders);

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
