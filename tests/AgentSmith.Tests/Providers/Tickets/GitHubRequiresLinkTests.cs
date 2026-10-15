using System.Net;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Octokit;
using Octokit.Internal;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-10-14-dbcem: GitHub's own blocked-by first, naming the blocking issue by its internal id; a GitHub that refuses
/// the dependency API gets one "Agent Smith — requires" comment on the dependent instead.
/// </summary>
public sealed class GitHubRequiresLinkTests
{
    private const long RequiredInternalId = 2_900_000_041L;

    private readonly Mock<IGitHubClient> _client = new();
    private readonly Mock<IConnection> _connection = new();
    private readonly Mock<IIssueCommentsClient> _comments = new();
    private object? _posted;

    public GitHubRequiresLinkTests()
    {
        var issues = new Mock<IIssuesClient>();
        issues.Setup(i => i.Get("owner", "repo", 41)).ReturnsAsync(new SimpleJsonSerializer().Deserialize<Issue>(
            $$"""{ "id": {{RequiredInternalId}}, "number": 41, "html_url": "https://github.com/owner/repo/issues/41" }"""));
        issues.SetupGet(i => i.Comment).Returns(_comments.Object);
        _client.SetupGet(c => c.Issue).Returns(issues.Object);
        _client.SetupGet(c => c.Connection).Returns(_connection.Object);
        _comments.Setup(c => c.Create("owner", "repo", 42, It.IsAny<string>())).ReturnsAsync((IssueComment)null!);
    }

    [Fact]
    public async Task GitHubRequiresLink_Link_PostsBlockedByWithRequiredIssueInternalId()
    {
        BlockedByAnswers(HttpStatusCode.Created);

        var result = await Link();

        result.Should().Be(RequiresLinkResult.Done);
        _connection.Verify(c => c.Post(new Uri("repos/owner/repo/issues/42/dependencies/blocked_by", UriKind.Relative),
            It.IsAny<object>(), "application/vnd.github+json", It.IsAny<CancellationToken>()), Times.Once);
        _posted.Should().BeOfType<Dictionary<string, object>>().Which.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, object>("issue_id", RequiredInternalId), "the id, never the number");
        _comments.Verify(c => c.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GitHubRequiresLink_BlockedByAnswers404_FallsBackToAgentSmithRequiresComment()
    {
        BlockedByThrows(new NotFoundException("Not Found", HttpStatusCode.NotFound));

        var result = await Link();

        result.Should().Be(RequiresLinkResult.Done);
        _comments.Verify(c => c.Create("owner", "repo", 42, "Agent Smith — requires #41 (must merge first)"), Times.Once);
    }

    [Fact]
    public async Task GitHubRequiresLink_BlockedByAnswers422_FallsBackToComment()
    {
        BlockedByThrows(new ApiException("Validation Failed", (HttpStatusCode)422));

        (await Link()).Should().Be(RequiresLinkResult.Done);
        _comments.Verify(c => c.Create("owner", "repo", 42, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GitHubRequiresLink_FallbackCommentFails_Refused()
    {
        BlockedByThrows(new ApiException("Gone", HttpStatusCode.Gone));
        _comments.Setup(c => c.Create("owner", "repo", 42, It.IsAny<string>()))
            .ThrowsAsync(new ApiException("Must have admin rights to Repository.", HttpStatusCode.Forbidden));

        var result = await Link();

        result.Should().Be(RequiresLinkResult.Refused("the dependency API is not available here, and the comment: not authorized"));
    }

    [Fact]
    public void GitHubRequiresLink_Comment_IsOursForEveryThreadReader()
    {
        var comment = GitHubRequiresLink.Comment(41);

        OwnTicketComment.IsOurs(comment).Should().BeTrue("a comment of ours is never a trigger");
        OwnTicketComment.IsOurs(new AgentSmith.Domain.Entities.TicketComment("sample.user", DateTimeOffset.UnixEpoch, comment))
            .Should().BeTrue();
    }

    [Fact]
    public async Task GitHubRequiresLink_TrackerUnreachable_UnreachedNeverThrows()
    {
        BlockedByThrows(new HttpRequestException("No such host is known. (api.github.example:443)"));

        (await Link()).Should().Be(RequiresLinkResult.Unreached("the tracker did not answer"));
        _comments.Verify(c => c.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GitHubRequiresLink_Refused_ReasonCarriesNoHostOrSdkText()
    {
        BlockedByThrows(new ApiException("Not Found", HttpStatusCode.NotFound));
        _comments.Setup(c => c.Create("owner", "repo", 42, It.IsAny<string>()))
            .ThrowsAsync(new ApiException("Validation failed for https://github.example/sample-org/repo", HttpStatusCode.BadRequest));

        var result = await Link();

        result.Reason.Should().Be("the dependency API is not available here, and the comment: the tracker refused the link (HTTP 400)");
        result.Reason.Should().NotContain("sample-org");
    }

    [Fact]
    public async Task GitHubRequiresLink_NonNumericId_RefusedWithoutACall()
    {
        var result = await Sut().LinkAsync(new TicketId("PROJ-1"), new TicketId("41"), CancellationToken.None);

        result.Linked.Should().BeFalse();
        _client.VerifyGet(c => c.Issue, Times.Never);
    }

    private Task<RequiresLinkResult> Link() => Sut().LinkAsync(new TicketId("42"), new TicketId("41"), CancellationToken.None);

    private GitHubRequiresLink Sut() =>
        new(_client.Object, new GitHubTicketConnection("https://github.com/owner/repo", "token"), NullLogger.Instance);

    private void BlockedByAnswers(HttpStatusCode status) =>
        _connection.Setup(c => c.Post(It.IsAny<Uri>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Uri, object, string, CancellationToken>((_, body, _, _) => _posted = body)
            .ReturnsAsync(status);

    private void BlockedByThrows(Exception failure) =>
        _connection.Setup(c => c.Post(It.IsAny<Uri>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
}
