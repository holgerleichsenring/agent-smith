using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// p0500: decides which branch a repository is READ on, for every remote source
/// provider, in one place.
/// <para>
/// config/agentsmith.example.yml states the contract — "default_branch comes from
/// each discovered repo; the connection value is only a fallback" — and all three
/// providers had it inverted, returning the configured value unconditionally and
/// asking the platform only when nothing was configured. A connection-level
/// <c>default_branch: develop</c> therefore blinded every repository that has no
/// develop: each read answered TF401175, so directory listings and file reads came
/// back empty, discovery saw an un-initialised repository, and init-project opened
/// no pull request. Nothing in that outcome mentioned a branch.
/// </para>
/// <para>
/// So the repository's own default wins, the configured value is the fallback it
/// was documented to be, and a disagreement between the two is stated at warning
/// level. The blinding was survivable; not being able to see it from the run's own
/// output was not.
/// </para>
/// </summary>
public sealed class DefaultBranchResolver(string? configuredBranch, string repoLabel, ILogger logger)
{
    /// <summary>Used only when neither the repository nor the configuration answers.</summary>
    public const string LastResort = "main";

    private const string RefsHeads = "refs/heads/";

    private string? _resolved;

    /// <summary>
    /// Resolves once and remembers the answer. <paramref name="askRepository"/> returns the
    /// repository's own default branch (with or without a refs/heads/ prefix), or null when
    /// the platform has no answer — an empty repository, or a call that failed.
    /// </summary>
    public async Task<string> ResolveAsync(
        Func<CancellationToken, Task<string?>> askRepository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(askRepository);
        return _resolved ??= Decide(await AskAsync(askRepository, cancellationToken));
    }

    private async Task<string?> AskAsync(
        Func<CancellationToken, Task<string?>> askRepository, CancellationToken cancellationToken)
    {
        try
        {
            return StripRefsHeads(await askRepository(cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Repo}: asking the platform for its default branch failed", repoLabel);
            return null;
        }
    }

    private string Decide(string? repositoryBranch)
    {
        if (repositoryBranch is not null) return Prefer(repositoryBranch);
        if (!string.IsNullOrEmpty(configuredBranch))
        {
            logger.LogInformation(
                "{Repo}: the platform named no default branch; using the configured '{Configured}'",
                repoLabel, configuredBranch);
            return configuredBranch;
        }
        logger.LogWarning(
            "{Repo}: neither the platform nor the configuration names a default branch; using '{Fallback}'",
            repoLabel, LastResort);
        return LastResort;
    }

    private string Prefer(string repositoryBranch)
    {
        // The one case worth a warning: somebody configured a branch, and the repository
        // disagrees. Before this was visible, the configured name silently won and every
        // read of a repository without it came back empty.
        if (!string.IsNullOrEmpty(configuredBranch)
            && !string.Equals(configuredBranch, repositoryBranch, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "{Repo}: configured default_branch '{Configured}' is not this repository's default "
                + "'{Actual}'. Reading '{Actual}' — the repository's own branch wins, the connection "
                + "value is only a fallback.",
                repoLabel, configuredBranch, repositoryBranch, repositoryBranch);
        }
        else
        {
            logger.LogDebug("{Repo}: default branch is '{Branch}'", repoLabel, repositoryBranch);
        }
        return repositoryBranch;
    }

    private static string? StripRefsHeads(string? branch) =>
        string.IsNullOrEmpty(branch) ? null
        : branch.StartsWith(RefsHeads, StringComparison.Ordinal) ? branch[RefsHeads.Length..]
        : branch;
}
