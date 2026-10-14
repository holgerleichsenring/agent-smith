using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Sweep;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.Rework;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

/// <summary>A comment command on a ticket branch's pull request is a rework act; elsewhere the model resolves it.</summary>
public sealed class PrCommentReworkCommandTests
{
    private const string PrUrl = "https://github.com/org/my-api/pull/42";
    private static readonly Dictionary<string, string> NoHeaders = new(StringComparer.OrdinalIgnoreCase);
    private readonly PrCommentHandlerFixture _fixture = new();

    private static IPrCommentAuthorTrust Trusted()
    {
        var trust = new Mock<IPrCommentAuthorTrust>();
        trust.Setup(t => t.IsTrustedAsync(It.IsAny<PrCommentAuthor>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return trust.Object;
    }

    private static PrCommentCommand Command(string body, string? headRef = "agent-smith/42", bool sameRepository = true) =>
        new(body, new PrCommentAuthor("https://github.com/org/my-api", "org/my-api", "alice", "alice"),
            "org/my-api#42", "pr:org/my-api#42", "42")
        {
            HeadRef = headRef, SameRepository = sameRepository, PrUrl = PrUrl,
        };

    private void VerifyNudgedOnce(string prUrl)
    {
        _fixture.Acts().Should().ContainSingle();
        _fixture.Acts()[0].Should().Be(new ReworkNudgeRequest("test-project", "42", ReworkNudgeOrigin.PullRequest, prUrl, ReworkChannel.PullRequest));
    }

    [Fact]
    public async Task PrCommentCommandAdmission_TicketBranchCommand_NudgesReworkWithoutModel()
    {
        var result = await _fixture.Admission().AdmitAsync(
            Command("/agentsmith rework the parser\nand keep the public API"), Trusted(), CancellationToken.None);

        result.Handled.Should().BeTrue();
        VerifyNudgedOnce(PrUrl);
        _fixture.VerifyModelNeverAsked();
        _fixture.Launched.Should().BeEmpty();
    }

    [Fact]
    public async Task PrCommentCommandAdmission_OurMarkedNote_IsIgnored()
    {
        var trust = new Mock<IPrCommentAuthorTrust>();

        var result = await _fixture.Admission().AdmitAsync(
            Command("<!-- agentsmith:handback -->\n/agent-smith"), trust.Object, CancellationToken.None);

        result.Handled.Should().BeFalse();
        _fixture.Nudged.Should().BeEmpty();
        trust.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PrCommentCommandAdmission_BareCommandElsewhere_IsNotHandled()
    {
        var result = await _fixture.Admission().AdmitAsync(Command("/agent-smith", headRef: "feature/x"), Trusted(), CancellationToken.None);

        result.Handled.Should().BeFalse();
        _fixture.VerifyModelNeverAsked();
        _fixture.Nudged.Should().BeEmpty();
    }

    [Fact]
    public async Task PrCommentCommandAdmission_ForkedTicketBranch_IsResolvedByTheModel()
    {
        await _fixture.Admission().AdmitAsync(Command("/agent-smith review", sameRepository: false), Trusted(), CancellationToken.None);

        _fixture.Nudged.Should().BeEmpty();
        _fixture.Launched.Should().ContainSingle();
    }

    [Fact]
    public async Task PrCommentCommandAdmission_TicketBranchWithoutOwner_IsNotHandled()
    {
        _fixture.TicketOwner = null;

        (await _fixture.Admission().AdmitAsync(Command("/agent-smith"), Trusted(), CancellationToken.None)).Handled.Should().BeFalse();
        _fixture.Nudged.Should().BeEmpty();
    }

    [Fact]
    public async Task CommentCommand_EachHostAndSweep_CarriesHeadRef()
    {
        (await new AzureDevOpsPrCommentWebhookHandler(_fixture.Admission(), Trusted(),
            NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance).HandleAsync(AzureComment, NoHeaders)).Handled.Should().BeTrue();
        (await new GitLabMrCommentWebhookHandler(_fixture.Admission(), Trusted(),
            NullLogger<GitLabMrCommentWebhookHandler>.Instance).HandleAsync(GitLabNote, NoHeaders)).Handled.Should().BeTrue();
        (await GitHub().HandleAsync(GitHubReviewComment, NoHeaders)).Handled.Should().BeTrue();
        _fixture.HeadRef = "agent-smith/42";
        (await GitHub().HandleAsync(GitHubIssueComment, NoHeaders)).Handled.Should().BeTrue();
        (await Sweep().CommandAsync(new SweepTarget(new AgentSmithConfig(), new RepoConnection { Name = "my-api", Type = RepoType.GitHub },
                new HashSet<string> { "test-project" }),
            new PrSweepComment("42", "1", DateTimeOffset.UnixEpoch, "/as", new PrCommentAuthor("u", "r", "alice", "alice")),
            new OpenPullRequest("42", PrUrl, null, null, "agent-smith/42", null, [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)
            {
                SameRepository = true,
            }, CancellationToken.None)).Handled.Should().BeTrue();

        _fixture.Acts().Select(n => n.PrUrl).Should().Equal(
            "https://dev.azure.com/org/MyProject/_git/my-api/pullrequest/42",
            "https://gitlab.example.com/org/my-api/-/merge_requests/42",
            PrUrl, PrUrl, PrUrl);
        _fixture.Nudged.Should().OnlyContain(n => n.TicketId == "42");
    }

    [Fact]
    public async Task PrTicketOwner_ProjectOutsideSweep_IsNotNudged()
    {
        var attempts = new Mock<IPreviousAttemptReader>();
        attempts.Setup(a => a.LatestAsync("app", "42", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", "success", DateTimeOffset.UnixEpoch, true));
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["app"] = new() { Name = "app", Repos = [new RepoConnection { Name = "app", Url = "https://github.com/o/app", Type = RepoType.GitHub }] },
            },
        };
        var owner = PrReworkAdmissionTests.Owner(attempts.Object, config);

        (await owner.FindAsync("https://github.com/o/app", "42", new HashSet<string> { "other" }, CancellationToken.None)).Should().BeNull();
        (await owner.FindAsync("https://github.com/o/app", "42", new HashSet<string> { "app" }, CancellationToken.None))!
            .ProjectName.Should().Be("app");
    }

    private GitHubPrCommentWebhookHandler GitHub() =>
        new(_fixture.Admission(), Trusted(), _fixture.Heads(), NullLogger<GitHubPrCommentWebhookHandler>.Instance);

    private PrSweepActions Sweep() =>
        new(new PrTriggerLabelResolver(), new PrRunContextFactory(), Mock.Of<IDetachedPipelineLauncher>(), _fixture.Admission(),
            new ServiceCollection().AddKeyedSingleton("github", Trusted()).BuildServiceProvider());

    private const string AzureComment = """
        { "eventType": "ms.vss-code.git-pullrequest-comment-event",
          "resource": {
            "comment": { "content": "/agent-smith", "author": { "id": "a-1", "uniqueName": "alice@example.com" } },
            "pullRequest": { "pullRequestId": 42, "sourceRefName": "refs/heads/agent-smith/42",
              "repository": { "id": "r-1", "name": "my-api", "remoteUrl": "https://org@dev.azure.com/org/MyProject/_git/my-api",
                "project": { "id": "p-1", "name": "MyProject" } } } } }
        """;

    private const string GitLabNote = """
        { "object_kind": "note", "user": { "id": 42, "username": "alice" },
          "project": { "id": 7, "path_with_namespace": "org/my-api", "web_url": "https://gitlab.example.com/org/my-api" },
          "object_attributes": { "id": 200, "note": "/agentsmith\nplease also rename the module", "noteable_type": "MergeRequest" },
          "merge_request": { "iid": 42, "source_branch": "agent-smith/42", "source_project_id": 7, "target_project_id": 7,
            "url": "https://gitlab.example.com/org/my-api/-/merge_requests/42" } }
        """;

    private const string GitHubReviewComment = """
        { "action": "created",
          "comment": { "id": 101, "body": "/as", "user": { "login": "alice" }, "author_association": "MEMBER" },
          "pull_request": { "number": 42, "html_url": "https://github.com/org/my-api/pull/42",
            "head": { "ref": "agent-smith/42", "repo": { "full_name": "org/my-api" } } },
          "repository": { "full_name": "org/my-api", "html_url": "https://github.com/org/my-api" } }
        """;

    private const string GitHubIssueComment = """
        { "action": "created",
          "comment": { "id": 100, "body": "/agent-smith", "user": { "login": "alice" }, "author_association": "MEMBER" },
          "issue": { "number": 42, "pull_request": { "url": "x" } },
          "repository": { "full_name": "org/my-api", "html_url": "https://github.com/org/my-api" } }
        """;
}
