using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitLabMrCommentWebhookHandlerTests
{
    private const string RepoUrl = "https://gitlab.example.com/org/my-api";
    private const int Developer = 30;
    private const int Reporter = 20;

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly PrCommentHandlerFixture _fixture = new();
    private readonly Mock<IGitLabMemberAccessReader> _members = new();

    private GitLabMrCommentWebhookHandler CreateSut(string? configuredUrl = RepoUrl) =>
        new(_fixture.Admission(),
            new GitLabMemberAccessTrust(PrCommentHandlerFixture.Repos(configuredUrl), _members.Object,
                PrCommentHandlerFixture.NewCache(), NullLogger<GitLabMemberAccessTrust>.Instance),
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);

    private void AccessLevel(int? level) =>
        _members.Setup(m => m.ReadAccessLevelAsync(It.Is<RepoConnection>(r => r.Url == RepoUrl), "7", "42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(level);

    private static string Note(string body, string noteableType = "MergeRequest") => $$"""
        {
            "object_kind": "note",
            "user": { "id": 42, "username": "dev-user" },
            "project": { "id": 7, "path_with_namespace": "org/my-api", "web_url": "{{RepoUrl}}" },
            "object_attributes": { "id": 200, "note": "{{body}}", "noteable_type": "{{noteableType}}" },
            "merge_request": { "iid": 15 }
        }
        """;

    [Fact]
    public void CanHandle_CorrectEventTypes()
    {
        var sut = CreateSut();

        sut.CanHandle("gitlab", "note hook").Should().BeTrue();
        sut.CanHandle("gitlab", "merge_request").Should().BeFalse();
        sut.CanHandle("github", "note hook").Should().BeFalse();
    }

    [Fact]
    public async Task GitLabMrComment_FromDeveloper_StartsPipeline()
    {
        AccessLevel(Developer);

        var result = await CreateSut().HandleAsync(Note("/agent-smith fix #99 in core"), EmptyHeaders);

        result.Handled.Should().BeTrue();
        _fixture.LaunchedAs().Should().Be("code #99 mr:org/my-api!15");
    }

    [Fact]
    public async Task GitLabMrComment_SecurityScan_FromDeveloper_StartsSecurityPipeline()
    {
        AccessLevel(Developer);

        var result = await CreateSut().HandleAsync(Note("/agent-smith security-scan"), EmptyHeaders);

        result.Handled.Should().BeTrue();
        _fixture.Launched.Single().Request.PipelineName.Should().Be("security-scan");
    }

    [Fact]
    public async Task GitLabMrComment_FromReporter_IsNotHandled_AndParserNeverCalled()
    {
        AccessLevel(Reporter);

        var result = await CreateSut().HandleAsync(Note("/agent-smith fix"), EmptyHeaders);

        result.Handled.Should().BeFalse();
        _fixture.VerifyModelNeverAsked();
    }

    [Fact]
    public async Task GitLabMrComment_FromNonMember_IsNotHandled()
    {
        AccessLevel(null);

        var result = await CreateSut().HandleAsync(Note("/agent-smith fix"), EmptyHeaders);

        result.Handled.Should().BeFalse();
        _fixture.VerifyModelNeverAsked();
    }

    [Fact]
    public async Task GitLabMrComment_MemberLookupFails_IsNotHandled()
    {
        _members.Setup(m => m.ReadAccessLevelAsync(
                It.IsAny<RepoConnection>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("gitlab down"));

        var result = await CreateSut().HandleAsync(Note("/agent-smith fix"), EmptyHeaders);

        result.Handled.Should().BeFalse();
        _fixture.VerifyModelNeverAsked();
    }

    [Fact]
    public async Task GitLabMrComment_RepoNotConfigured_IsNotHandled()
    {
        AccessLevel(Developer);

        var result = await CreateSut(configuredUrl: null).HandleAsync(Note("/agent-smith fix"), EmptyHeaders);

        result.Handled.Should().BeFalse();
        _fixture.VerifyModelNeverAsked();
    }

    [Theory]
    [InlineData("Just a regular comment")]
    [InlineData("/agent-smith help")]
    [InlineData("/approve looks good")]
    public async Task GitLabMrComment_WithoutACommand_IsNotHandled_AndCostsNoLookup(string body)
    {
        var result = await CreateSut().HandleAsync(Note(body), EmptyHeaders);

        result.Handled.Should().BeFalse();
        _members.VerifyNoOtherCalls();
        _fixture.VerifyModelNeverAsked();
    }

    [Fact]
    public async Task GitLabMrComment_OnAnIssue_IsNotHandled()
    {
        var result = await CreateSut().HandleAsync(Note("/agent-smith fix", "Issue"), EmptyHeaders);

        result.Handled.Should().BeFalse();
    }
}
