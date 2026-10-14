using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Sweep;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

/// <summary>
/// Every comment on a ticket's pull request but our own note refreshes its record before the command
/// match — on every host's webhook and in the PR sweep; a command still carries its act.
/// </summary>
public sealed class PrCommentRecordNudgeTests : IDisposable
{
    private const string PrUrl = "https://github.com/org/my-api/pull/42";
    private static readonly Dictionary<string, string> NoHeaders = new(StringComparer.OrdinalIgnoreCase);
    private readonly PrCommentHandlerFixture _fixture = new();
    private readonly ServerStateStore _store = new();

    public void Dispose() => _store.Dispose();

    private static IPrCommentAuthorTrust Trusted()
    {
        var trust = new Mock<IPrCommentAuthorTrust>();
        trust.Setup(t => t.IsTrustedAsync(It.IsAny<PrCommentAuthor>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return trust.Object;
    }

    /// <summary>The real head rule: only a command's head is read; a plain comment keeps none.</summary>
    private static IPrCommandHead HeadsForCommandsOnly()
    {
        var heads = new Mock<IPrCommandHead>();
        heads.Setup(h => h.CompleteAsync(It.IsAny<PrCommentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrCommentCommand c, CancellationToken _) => c);
        return heads.Object;
    }

    private GitHubPrCommentWebhookHandler GitHub(IPrCommandHead? heads = null) =>
        new(_fixture.Admission(), Trusted(), heads ?? _fixture.Heads(), NullLogger<GitHubPrCommentWebhookHandler>.Instance);

    [Fact]
    public async Task PrComment_PlainCommentOnTicketBranch_NudgesRefresh_EveryHost()
    {
        await new AzureDevOpsPrCommentWebhookHandler(_fixture.Admission(), Trusted(), NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance)
            .HandleAsync(AzureComment, NoHeaders);
        await new GitLabMrCommentWebhookHandler(_fixture.Admission(), Trusted(), NullLogger<GitLabMrCommentWebhookHandler>.Instance)
            .HandleAsync(GitLabNote, NoHeaders);
        await GitHub().HandleAsync(GitHubReviewComment, NoHeaders);

        _fixture.Nudged.Select(n => n.PrUrl).Should().Equal(
            "https://dev.azure.com/org/MyProject/_git/my-api/pullrequest/42",
            "https://gitlab.example.com/org/my-api/-/merge_requests/42", PrUrl);
        _fixture.Nudged.Should().OnlyContain(n => n.Origin == ReworkNudgeOrigin.Refresh && n.TicketId == "42" && n.FromDelivery);
        _fixture.VerifyModelNeverAsked();
        _fixture.Launched.Should().BeEmpty();
    }

    [Fact]
    public async Task PrComment_OurOwnNote_NudgesNothing()
    {
        await GitHub().HandleAsync(GitHubReviewComment.Replace("looks good", "<!-- agentsmith:handback -->\\nthe hand-back"), NoHeaders);

        _fixture.Nudged.Should().BeEmpty();
    }

    [Fact]
    public async Task GitHubIssueComment_NoHead_NudgesTheTicketOfItsRecord()
    {
        await using (var ctx = _store.Context())
        {
            ctx.Add(new PrReviewRecord { PrKey = PrKey.Of(PrUrl), Url = PrUrl, Project = "owner-project", TicketId = "42", RunId = "run-1", State = "open" });
            await ctx.SaveChangesAsync();
        }
        _fixture.Scopes = _store.ScopeFactory;

        await GitHub(HeadsForCommandsOnly()).HandleAsync(GitHubIssueComment, NoHeaders);

        _fixture.Nudged.Should().ContainSingle().Which.Should().Match<ReworkNudgeRequest>(n => n.Project == "owner-project"
            && n.TicketId == "42" && n.PrUrl == PrUrl && n.Origin == ReworkNudgeOrigin.Refresh && n.FromDelivery);
    }

    [Fact]
    public async Task PrComment_TrustedCommand_StillNudgesPullRequestOrigin()
    {
        (await GitHub().HandleAsync(GitHubReviewComment.Replace("looks good", "/agent-smith"), NoHeaders)).Handled.Should().BeTrue();

        _fixture.Nudged.Select(n => n.Origin).Should().Equal(ReworkNudgeOrigin.Refresh, ReworkNudgeOrigin.PullRequest);
        _fixture.Acts().Single().Channel.Should().Be(ReworkChannel.PullRequest);
    }

    [Fact]
    public async Task PrSweep_PlainComment_NudgesRefresh()
    {
        var sweep = new PrSweepActions(new PrTriggerLabelResolver(), new PrRunContextFactory(), Mock.Of<IDetachedPipelineLauncher>(),
            _fixture.Admission(), new ServiceCollection().AddKeyedSingleton("github", Trusted()).BuildServiceProvider());

        await sweep.CommandAsync(new SweepTarget(new AgentSmithConfig(), new RepoConnection { Name = "my-api", Type = RepoType.GitHub },
                new HashSet<string> { "test-project" }),
            new PrSweepComment("42", "1", DateTimeOffset.UnixEpoch, "looks good", new PrCommentAuthor("u", "r", "alice", "alice")),
            new OpenPullRequest("42", PrUrl, null, null, "agent-smith/42", null, [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)
            {
                SameRepository = true,
            }, CancellationToken.None);

        _fixture.Nudged.Should().ContainSingle().Which.Should().Match<ReworkNudgeRequest>(n => n.Origin == ReworkNudgeOrigin.Refresh
            && n.PrUrl == PrUrl && !n.FromDelivery && n.IsLate);
    }

    private const string AzureComment = """
        { "eventType": "ms.vss-code.git-pullrequest-comment-event",
          "resource": {
            "comment": { "content": "looks good", "author": { "id": "a-1", "uniqueName": "alice@example.com" } },
            "pullRequest": { "pullRequestId": 42, "sourceRefName": "refs/heads/agent-smith/42",
              "repository": { "id": "r-1", "name": "my-api", "remoteUrl": "https://org@dev.azure.com/org/MyProject/_git/my-api",
                "project": { "id": "p-1", "name": "MyProject" } } } } }
        """;

    private const string GitLabNote = """
        { "object_kind": "note", "user": { "id": 42, "username": "alice" },
          "project": { "id": 7, "path_with_namespace": "org/my-api", "web_url": "https://gitlab.example.com/org/my-api" },
          "object_attributes": { "id": 200, "note": "looks good", "noteable_type": "MergeRequest" },
          "merge_request": { "iid": 42, "source_branch": "agent-smith/42", "source_project_id": 7, "target_project_id": 7,
            "url": "https://gitlab.example.com/org/my-api/-/merge_requests/42" } }
        """;

    private const string GitHubReviewComment = """
        { "action": "created",
          "comment": { "id": 101, "body": "looks good", "path": "src/a.cs", "line": 3, "user": { "login": "alice" }, "author_association": "MEMBER" },
          "pull_request": { "number": 42, "html_url": "https://github.com/org/my-api/pull/42",
            "head": { "ref": "agent-smith/42", "repo": { "full_name": "org/my-api" } } },
          "repository": { "full_name": "org/my-api", "html_url": "https://github.com/org/my-api" } }
        """;

    private const string GitHubIssueComment = """
        { "action": "created",
          "comment": { "id": 100, "body": "thanks, merging tomorrow", "user": { "login": "alice" }, "author_association": "MEMBER" },
          "issue": { "number": 42, "pull_request": { "url": "x", "html_url": "https://github.com/org/my-api/pull/42" } },
          "repository": { "full_name": "org/my-api", "html_url": "https://github.com/org/my-api" } }
        """;
}
