using FluentAssertions;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>2026-11-24-5ee0: only the token's own open threads that start with the marker are set Fixed.</summary>
public sealed class AzureReposMarkedThreadsTests
{
    private const string Marker = "<!-- agent-smith:base-refresh -->";
    private const string Me = "11111111-1111-1111-1111-111111111111";
    private readonly Mock<GitHttpClient> _git = new(new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
    private readonly List<int> _fixed = [];

    [Fact]
    public async Task ResolveAsync_OwnActiveMarkedThread_IsFixed_OthersLeft()
    {
        Threads(
            Thread(1, CommentThreadStatus.Active, Me, $"{Marker}\nconflicts"),
            Thread(2, CommentThreadStatus.Active, "someone-else", $"{Marker}\npasted"),
            Thread(3, CommentThreadStatus.Fixed, Me, $"{Marker}\nolder"),
            Thread(4, CommentThreadStatus.Active, Me, "a review note"),
            Thread(5, CommentThreadStatus.Pending, Me, $"{Marker}\nred"));

        var resolved = await Sut(Me).ResolveAsync("7", Marker, CancellationToken.None);

        resolved.Should().Be(2);
        _fixed.Should().Equal(1, 5);
    }

    [Fact]
    public async Task ResolveAsync_UnknownToken_ResolvesNothing()
    {
        Threads(Thread(1, CommentThreadStatus.Active, Me, Marker));

        (await Sut(null).ResolveAsync("7", Marker, CancellationToken.None)).Should().Be(0);
        _fixed.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_OneThreadRefused_TheOthersAreStillResolved()
    {
        Threads(Thread(1, CommentThreadStatus.Active, Me, Marker), Thread(2, CommentThreadStatus.Active, Me, Marker));
        _git.Setup(c => c.UpdateThreadAsync(It.IsAny<GitPullRequestCommentThread>(), "demo", "repo", 7, 1, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TF401181"));

        (await Sut(Me).ResolveAsync("7", Marker, CancellationToken.None)).Should().Be(1);
        _fixed.Should().Equal(2);
    }

    private AzureReposMarkedThreads Sut(string? self) =>
        new(_ => Task.FromResult(_git.Object), _ => Task.FromResult(self), "demo", "repo", NullLogger.Instance);

    private void Threads(params GitPullRequestCommentThread[] threads)
    {
        _git.Setup(c => c.GetThreadsAsync("demo", "repo", 7, It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. threads]);
        _git.Setup(c => c.UpdateThreadAsync(It.Is<GitPullRequestCommentThread>(t => t.Status == CommentThreadStatus.Fixed),
                "demo", "repo", 7, It.IsAny<int>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<GitPullRequestCommentThread, string, string, int, int, object, CancellationToken>((_, _, _, _, id, _, _) => _fixed.Add(id))
            .ReturnsAsync(new GitPullRequestCommentThread());
    }

    private static GitPullRequestCommentThread Thread(int id, CommentThreadStatus status, string author, string content) => new()
    {
        Id = id, Status = status,
        Comments = [new Comment { Id = 1, Content = content, Author = new IdentityRef { Id = author } }],
    };
}
