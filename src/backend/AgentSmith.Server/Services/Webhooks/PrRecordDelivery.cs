namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// A change on a pull request that is no act — a review, a vote, a comment, a merge, a close or a
/// reopen — as a delivery or the PR sweep names it: the repository, the head branch and whether it
/// lives in the same repository (null when the delivery carries no head), the pull request's URL,
/// the projects a polling sweep is limited to, and whether a webhook delivered it.
/// </summary>
public sealed record PrRecordDelivery(
    string RepoUrl,
    string? HeadRef,
    bool SameRepository,
    string? PrUrl,
    IReadOnlySet<string>? AllowedProjects = null,
    bool FromDelivery = true)
{
    public static PrRecordDelivery Of(PrReviewRequest request) =>
        new(request.RepoUrl, request.HeadRef, request.SameRepository, request.PrUrl);

    public static PrRecordDelivery Of(PrCommentCommand command, IReadOnlySet<string>? allowedProjects) =>
        new(command.Author.RepositoryUrl, command.HeadRef, command.SameRepository, command.PrUrl, allowedProjects, !command.FromSweep);
}
