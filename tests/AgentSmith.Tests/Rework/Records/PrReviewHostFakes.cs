using AgentSmith.Tests.TestSupport;
using System.Net;
using System.Text;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>2026-10-13-9a59b: the three hosts' answers, scripted.</summary>
internal static class PrReviewHostFakes
{
    public static IGitHubClient GitHubReplying(string reply) => GitHubGraphQlHost.Replying(reply);

    public static HttpClient GitLabAnswering(Func<string, string> body) => new(new Routed(body));

    public static GitPullRequestCommentThread VoteThread(int id, string voter, short vote, DateTime at) => new()
    {
        Id = id,
        PublishedDate = at,
        Properties = new PropertiesCollection { ["CodeReviewThreadType"] = "VoteUpdate", ["CodeReviewVoteResult"] = vote.ToString() },
        Comments = [new Comment { Id = 1, Author = new IdentityRef { Id = voter, UniqueName = voter }, CommentType = CommentType.System, Content = $"{voter} voted {vote}" }],
    };

    public static GitPullRequest AzurePr(PullRequestStatus status, params IdentityRefWithVote[] reviewers) => new()
    {
        PullRequestId = 12,
        Status = status,
        CreationDate = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
        ClosedDate = new DateTime(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc),
        CreatedBy = new IdentityRef { Id = "creator" },
        Reviewers = reviewers,
    };

    private sealed class Routed(Func<string, string> body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body(request.RequestUri!.AbsolutePath), Encoding.UTF8, "application/json"),
            });
    }
}
