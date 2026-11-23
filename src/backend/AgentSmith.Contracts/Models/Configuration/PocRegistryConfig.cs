using System.Text.RegularExpressions;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-933de: the container registry of a PoC target, from <c>pocs.targets[].registry</c>. Images are
/// &lt;login_server&gt;/&lt;repository&gt;/&lt;poc&gt;; agent smith reads them with ONE pull-only token
/// (<see cref="PullUser"/> + the catalog secret <see cref="PullAuth"/>) and holds no push or delete credential.
/// <see cref="PullMode"/> says whether 2026-11-23-933dg delivers that token into each PoC namespace as an
/// imagePullSecret or the cluster can already pull. Not the top-level <c>registries:</c> list — those are package feeds.
/// </summary>
public sealed partial class PocRegistryConfig
{
    public const string Key = "pocs.targets[].registry";
    public const string Acr = "acr";
    public const string PullSecretMode = "pull_secret";
    public const string ClusterPullsMode = "cluster_pulls";

    public string Type { get; set; } = Acr;

    /// <summary>The registry's host, e.g. 'pocregistry.azurecr.io'.</summary>
    public string LoginServer { get; set; } = string.Empty;

    /// <summary>The repository prefix every PoC image lives under.</summary>
    public string Repository { get; set; } = "pocs";

    public string PullUser { get; set; } = string.Empty;

    /// <summary>The catalog secret holding the pull-only token.</summary>
    public string PullAuth { get; set; } = string.Empty;

    public string PullMode { get; set; } = PullSecretMode;

    /// <summary>What is wrong with these values, one sentence each naming the key, the value and the reason. Local values
    /// only: whether <see cref="PullAuth"/> names a catalog secret is a load finding.</summary>
    public IEnumerable<string> Problems()
    {
        if (!string.Equals(Type, Acr, StringComparison.Ordinal))
            yield return $"{Key}.type is '{Type}'; it must be '{Acr}', the one container registry agent smith reads so far";
        else if (!AcrLoginServer().IsMatch(LoginServer))
            yield return $"{Key}.login_server is '{LoginServer}'; an ACR login server is '<name>.azurecr.io' (5 to 50 lower-case "
                + "letters and digits, optionally '-<hash>', or the .azurecr.us and .azurecr.cn clouds), because the pull "
                + "token is only ever sent to that host";
        if (!RepositoryPath().IsMatch(Repository))
            yield return $"{Key}.repository is '{Repository}'; it must be a registry path (lower-case letters and digits, "
                + "separated by '.', '_', '-' or '/'), because every PoC image is <repository>/<poc>";
        if (string.IsNullOrWhiteSpace(PullUser))
            yield return $"{Key}.pull_user is empty; name the user of the pull-only token, because the registry signs in with it";
        if (string.IsNullOrWhiteSpace(PullAuth))
            yield return $"{Key}.pull_auth is empty; name the catalog secret of the pull-only token, because agent smith "
                + "resolves and verifies every PoC image with it, also with pull_mode cluster_pulls";
        if (PullMode is not (PullSecretMode or ClusterPullsMode))
            yield return $"{Key}.pull_mode is '{PullMode}'; it must be '{PullSecretMode}' or '{ClusterPullsMode}', because "
                + "it decides whether a pull secret is delivered into each PoC namespace";
    }

    [GeneratedRegex(@"^[a-z0-9]{5,50}(-[a-z0-9]{1,63})?\.azurecr\.(io|us|cn)$")]
    private static partial Regex AcrLoginServer();

    [GeneratedRegex(@"^[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*(/[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*)*$")]
    private static partial Regex RepositoryPath();
}
