using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-9a59b: the trust verdict of each review author asked once per refresh — a reviewer who
/// wrote twenty comments costs one membership read, not twenty.
/// </summary>
public sealed class PrReviewAuthorTrustOnce(IPrReviewAuthorTrust trust)
{
    private readonly Dictionary<(RepoType, string, string), bool> _verdicts = [];

    public async Task<bool> IsTrustedAsync(RepoType host, PrCommentAuthor author, CancellationToken ct)
    {
        var key = (host, author.RepositoryId, author.AuthorId);
        if (_verdicts.TryGetValue(key, out var known)) return known;
        return _verdicts[key] = await trust.IsTrustedAsync(host, author, ct);
    }
}
