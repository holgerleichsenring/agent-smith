namespace AgentSmith.Contracts.Webhooks;

/// <summary>
/// Who wrote a pull-request / merge-request comment, and where, in the terms the code host's
/// own webhook payload uses.
/// </summary>
/// <param name="RepositoryUrl">The repository's URL, matched against the configured projects' repos.</param>
/// <param name="RepositoryId">The host's id for the repository (GitLab project id, Azure DevOps repository id, GitHub full name).</param>
/// <param name="AuthorId">The host's id for the author (GitLab user id, Azure DevOps identity id, GitHub login).</param>
/// <param name="AuthorLogin">The author's display handle, for logs.</param>
public sealed record PrCommentAuthor(
    string RepositoryUrl,
    string RepositoryId,
    string AuthorId,
    string AuthorLogin)
{
    /// <summary>The Azure DevOps project id the repository belongs to; unused elsewhere.</summary>
    public string? ProjectId { get; init; }

    /// <summary>GitHub's <c>author_association</c> as the payload states it; unused elsewhere.</summary>
    public string? Association { get; init; }
}
