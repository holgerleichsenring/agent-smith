namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-25-9dbbf: the review queries — the first read of a pull request's three connections, one
/// follow-up per connection with a cursor, and a thread's further comments through node(id:).
/// </summary>
public static class GitHubPrReviewQueries
{
    private const string Page = "pageInfo{hasNextPage endCursor}";
    private const string Note = "id author{__typename login} body createdAt publishedAt";
    private const string Review = "id author{__typename login} body submittedAt";
    private const string Comment = "id author{__typename login} body createdAt";
    private const string Thread = "id isResolved path line originalLine comments(first:100){" + Page + " nodes{" + Note + "}}";
    private const string PullRequest =
        "repository(owner:$owner,name:$repo){pullRequest(number:$number){";
    private const string FollowUpHead = "query($owner:String!,$repo:String!,$number:Int!,$after:String){" + PullRequest;

    public const string First =
        "query($owner:String!,$repo:String!,$number:Int!){viewer{login} " + PullRequest
        + "reviewThreads(first:100){" + Page + " nodes{" + Thread + "}}"
        + "reviews(first:100){" + Page + " nodes{" + Review + "}}"
        + "comments(first:100){" + Page + " nodes{" + Comment + "}}}}}";

    public const string Threads = FollowUpHead + "reviewThreads(first:100,after:$after){" + Page + " nodes{" + Thread + "}}}}}";
    public const string Reviews = FollowUpHead + "reviews(first:100,after:$after){" + Page + " nodes{" + Review + "}}}}}";
    public const string Comments = FollowUpHead + "comments(first:100,after:$after){" + Page + " nodes{" + Comment + "}}}}}";

    public const string ThreadComments =
        "query($id:ID!,$after:String){node(id:$id){... on PullRequestReviewThread{comments(first:100,after:$after){"
        + Page + " nodes{" + Note + "}}}}}";
}
