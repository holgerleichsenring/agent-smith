using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-d5cc: where a local repository's run gets its tree. A bind mount (Docker, a CLI source path) puts the
/// checkout at /work and is trusted as it is; the in-process sandbox's fresh work dir holds none, and is cloned from
/// the repo's url, else its path, exactly as a remote repo's sandbox is.
/// </summary>
internal static class LocalRepoCheckout
{
    public const string NothingToClone =
        "The sandbox holds no checkout and the local repo names neither a url nor a path to clone.";

    /// <summary>True when every sandbox's /work is a work tree's root: an empty prefix is only ever answered there.</summary>
    public static async Task<bool> IsBindMountedAsync(
        IReadOnlyList<KeyValuePair<string, ISandbox>> sandboxes, CancellationToken ct)
    {
        foreach (var (_, sandbox) in sandboxes)
        {
            var probe = await sandbox.RunStepAsync(CheckoutStepFactory.BuildIsWorkTreeStep(), null, ct);
            if (probe.ExitCode != 0 || !string.IsNullOrWhiteSpace(probe.OutputContent)) return false;
        }
        return true;
    }

    /// <summary>The repo with its clone source as its url: the url when it names one, else the path; null with neither.</summary>
    public static RepoConnection? CloneSource(RepoConnection config) =>
        !string.IsNullOrEmpty(config.Url) ? config
        : !string.IsNullOrEmpty(config.Path) ? config with { Url = config.Path }
        : null;
}
