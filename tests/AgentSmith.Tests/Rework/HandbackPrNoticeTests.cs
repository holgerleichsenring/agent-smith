using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>A hand-back is posted on the ticket's pull requests, opened by our marker.</summary>
public sealed class HandbackPrNoticeTests
{
    [Fact]
    public async Task PostAsync_PreviousAttemptsPullRequests_GetTheMarkedHandback()
    {
        var provider = new Mock<ISourceProvider>();
        var comments = provider.As<IPrCommentProvider>();
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repos, (IReadOnlyList<RepoConnection>)[new RepoConnection { Name = "api", Type = RepoType.GitHub }]);
        pipeline.Set(ContextKeys.PreviousAttempt, new PreviousAttempt("run-1", "success", DateTimeOffset.UnixEpoch, true)
        {
            PullRequestUrls = new Dictionary<string, string> { ["api"] = "https://github.com/o/api/pull/4", ["web"] = "https://github.com/o/web/pull/5" },
        });

        await new HandbackPrNotice(sources.Object, NullLogger<HandbackPrNotice>.Instance)
            .PostAsync(pipeline, "## Agent Smith — the ticket reads two ways", CancellationToken.None);

        comments.Verify(c => c.PostCommentAsync("4", "<!-- agentsmith:handback -->\n## Agent Smith — the ticket reads two ways",
            It.IsAny<CancellationToken>()), Times.Once);
        comments.VerifyNoOtherCalls();
    }
}
