using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// GitHub states the author's standing on the repository in the webhook payload itself
/// (<c>author_association</c>), so no API call is needed. OWNER, MEMBER and COLLABORATOR
/// can write; CONTRIBUTOR only means a commit of theirs was once merged.
/// </summary>
public sealed class GitHubAuthorAssociationTrust : IPrCommentAuthorTrust
{
    private static readonly HashSet<string> WriteAssociations = new(StringComparer.OrdinalIgnoreCase)
    {
        "OWNER",
        "MEMBER",
        "COLLABORATOR",
    };

    public Task<bool> IsTrustedAsync(PrCommentAuthor author, CancellationToken cancellationToken) =>
        Task.FromResult(author.Association is not null && WriteAssociations.Contains(author.Association));
}
