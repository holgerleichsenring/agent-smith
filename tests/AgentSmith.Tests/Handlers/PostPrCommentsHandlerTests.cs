using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0167c: PostPrCommentsHandler stamps every body with the agentsmith
/// pr-review marker, deletes the previous run's marked comments FIRST, then
/// posts the new batch via the repo's IPrCommentProvider — idempotent per
/// file + line on PR-synchronize.
/// </summary>
public sealed class PostPrCommentsHandlerTests
{
    private static readonly RepoConnection Repo = new()
    {
        Name = "primary",
        Type = RepoType.GitHub,
        Url = "https://github.com/org/my-api",
    };

    [Fact]
    public async Task PostPrComments_FirstRun_PostsAllWithMarker()
    {
        var (handler, provider, _) = CreateSut(deletedOnFirstPass: 0);
        PrReviewSummary? posted = null;
        provider.Setup(p => p.PostReviewBatchAsync("42", It.IsAny<PrReviewSummary>(), It.IsAny<CancellationToken>()))
            .Callback<string, PrReviewSummary, CancellationToken>((_, review, _) => posted = review)
            .Returns(Task.CompletedTask);

        var result = await handler.ExecuteAsync(Context(Review(
            ("src/A.cs", 3, 4, "Finding one"),
            ("src/B.cs", 7, 7, "Finding two"))), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        posted.Should().NotBeNull();
        posted!.TopLevelComment.Should().StartWith("<!-- agentsmith:pr-review:summary -->");
        posted.InlineComments[0].Body.Should().StartWith("<!-- agentsmith:pr-review:src/A.cs:3 -->");
        posted.InlineComments[0].Body.Should().EndWith("Finding one");
        posted.InlineComments[1].Body.Should().StartWith("<!-- agentsmith:pr-review:src/B.cs:7 -->");
    }

    [Fact]
    public async Task PostPrComments_SecondRun_DeletesMarkedThenPosts()
    {
        var (handler, _, calls) = CreateSut(deletedOnFirstPass: 3);

        var result = await handler.ExecuteAsync(
            Context(Review(("src/A.cs", 3, 4, "Finding"))), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        calls.Should().Equal("delete", "post");
        result.Message.Should().Contain("replaced 3");
    }

    [Fact]
    public async Task PostPrComments_ZeroFindings_PostsOnlySummaryWithCleanResult()
    {
        // Chain the real compiler: zero observations still produce a summary
        // reporting a clean review — PostPrComments always has a post to make.
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-42");
        var compile = new CompilePrReviewFindingsHandler(
            new PrReviewFindingSelector(), new PrReviewCommentRenderer(),
            NullLogger<CompilePrReviewFindingsHandler>.Instance);
        await compile.ExecuteAsync(
            new CompilePrReviewFindingsContext(new PrDiffAnalysis("base", "head", []), pipeline),
            CancellationToken.None);
        var review = pipeline.Get<PrReviewSummary>(ContextKeys.PrReviewSummary);

        var (handler, provider, _) = CreateSut(deletedOnFirstPass: 0);
        PrReviewSummary? posted = null;
        provider.Setup(p => p.PostReviewBatchAsync("42", It.IsAny<PrReviewSummary>(), It.IsAny<CancellationToken>()))
            .Callback<string, PrReviewSummary, CancellationToken>((_, r, _) => posted = r)
            .Returns(Task.CompletedTask);

        var result = await handler.ExecuteAsync(Context(review), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        posted!.InlineComments.Should().BeEmpty();
        posted.TopLevelComment.Should().StartWith("<!-- agentsmith:pr-review:summary -->")
            .And.Contain("No findings");
    }

    [Fact]
    public async Task PostPrComments_RepoWithoutPrCommentSupport_Fails()
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(Repo)).Returns(Mock.Of<ISourceProvider>());
        var handler = new PostPrCommentsHandler(
            factory.Object, NullLogger<PostPrCommentsHandler>.Instance);

        var result = await handler.ExecuteAsync(
            Context(Review(("src/A.cs", 1, 1, "Finding"))), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("no PR-comment support");
    }

    private static (PostPrCommentsHandler Handler, Mock<IPrCommentProvider> Provider, List<string> Calls)
        CreateSut(int deletedOnFirstPass)
    {
        var calls = new List<string>();
        var source = new Mock<ISourceProvider>();
        var provider = source.As<IPrCommentProvider>();
        provider.Setup(p => p.DeleteCommentsByMarkerAsync(
                "42", "<!-- agentsmith:pr-review:", It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("delete"))
            .ReturnsAsync(deletedOnFirstPass);
        provider.Setup(p => p.PostReviewBatchAsync("42", It.IsAny<PrReviewSummary>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("post"))
            .Returns(Task.CompletedTask);
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(Repo)).Returns(source.Object);
        var handler = new PostPrCommentsHandler(
            factory.Object, NullLogger<PostPrCommentsHandler>.Instance);
        return (handler, provider, calls);
    }

    private static PostPrCommentsContext Context(PrReviewSummary review)
        => new(Repo, "42", review, new PipelineContext());

    private static PrReviewSummary Review(
        params (string File, int Start, int End, string Body)[] comments) => new(
        "Summary body",
        comments.Select(c => new PrReviewInlineComment(
            c.File, c.Start, c.End, "High", "correctness", c.Body)).ToList());
}
