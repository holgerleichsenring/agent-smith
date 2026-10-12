using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Webhooks;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>The worker reads a trusted comment command beside the standing vote; the newer one is the act.</summary>
public sealed class ReworkPendingActsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly RepoConnection Repo = new() { Name = "api", Type = RepoType.GitHub };
    private readonly Mock<ISourceProvider> _provider = new();

    private static PrReviewNote Note(string who, int minutes, string body = "x") =>
        new(new PrCommentAuthor("u", "r", who, who), false, false, Start.AddMinutes(minutes), body);

    private Task<PendingReworkAct?> NewestAsync()
    {
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        var trust = new Mock<IPrReviewAuthorTrust>();
        trust.Setup(t => t.IsTrustedAsync(It.IsAny<RepoType>(), It.Is<PrCommentAuthor>(a => a.AuthorId != "mallory"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var project = new ResolvedProject { Name = "p", Repos = [Repo] };
        var attempt = new PreviousAttempt("run-1", "success", Start, true)
        {
            PullRequestUrls = new Dictionary<string, string> { ["api"] = "https://github.com/o/api/pull/4" },
        };
        return new ReworkPendingActs(Mock.Of<ITicketProviderFactory>(), sources.Object, trust.Object)
            .NewestAsync(project, "42", attempt, CancellationToken.None);
    }

    private void Review(IReadOnlyList<PrReviewNote> votes, params PrReviewNote[] notes)
    {
        _provider.As<IPrReviewActReader>().Setup(r => r.ChangesRequestedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(votes);
        _provider.As<IPrReviewThreadReader>().Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. notes.Select(n => new PrReviewThread(null, null, null, [n]))]);
    }

    [Fact]
    public async Task NewestAsync_CommandAfterVote_IsTheAct()
    {
        Review([Note("bob", 10)], Note("alice", 5, "rename it"), Note("alice", 20, "/agentsmith\nand the tests"));

        var act = await NewestAsync();

        act!.Act.Should().Be(new ReworkAct("alice", Start.AddMinutes(20), ReworkChannel.PullRequest));
        act.Repo.Should().Be(Repo);
    }

    [Fact]
    public async Task NewestAsync_UntrustedCommand_IsNoAct()
    {
        Review([], Note("mallory", 20, "/agent-smith"));

        (await NewestAsync()).Should().BeNull();
    }
}
