namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-c81ec: the Argo CD a PoC target is deployed by, from <c>pocs.targets[].argo</c>. Agent smith reads it
/// through its API with one AppProject role token (the catalog secret <see cref="TokenSecret"/>) and holds no cluster
/// credential; poc-target-install upserts the target's ApplicationSet there, and every PoC run follows its own rollout.
/// </summary>
public sealed class PocArgoConfig
{
    public const string Key = "pocs.targets[].argo";
    public const string DefaultNamespace = "argocd";
    public const string DefaultDestination = "https://kubernetes.default.svc";

    /// <summary>The Argo CD server's https URL, e.g. 'https://argocd.example.internal'.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The catalog secret holding the AppProject role JWT.</summary>
    public string TokenSecret { get; set; } = string.Empty;

    /// <summary>The AppProject every PoC Application belongs to.</summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>The namespace Argo CD runs in, where the ApplicationSet lives.</summary>
    public string Namespace { get; set; } = DefaultNamespace;

    /// <summary>The cluster the PoCs are deployed to, as Argo CD names it.</summary>
    public string DestinationServer { get; set; } = DefaultDestination;

    /// <summary>The source repo's URL as Argo CD's repository credential names it.</summary>
    public string RepoUrl { get; set; } = string.Empty;

    /// <summary>The ApplicationSet's name; empty reads 'pocs-&lt;target name&gt;'.</summary>
    public string? ApplicationSet { get; set; }

    /// <summary>How many days before the token's expiry the probe and the studio warn.</summary>
    public int TokenWarnDays { get; set; } = 14;

    /// <summary>How often the git generator looks for created and deleted PoC folders.</summary>
    public int GeneratorRequeueSeconds { get; set; } = 30;

    /// <summary>The ApplicationSet's name for <paramref name="target"/>.</summary>
    public string ApplicationSetName(string target) =>
        string.IsNullOrWhiteSpace(ApplicationSet) ? $"pocs-{target}" : ApplicationSet.Trim();

    /// <summary>What is wrong with these values, one sentence each naming the key, the value and the reason. Local values
    /// only: whether <see cref="TokenSecret"/> names a catalog secret is a load finding.</summary>
    public IEnumerable<string> Problems(string target)
    {
        string K(string field) => $"{Key}.{field} of target '{target}'";
        if (!IsHttps(Url))
            yield return $"{K("url")} is '{Url}'; it must be an https URL, because the role token is sent to it";
        if (string.IsNullOrWhiteSpace(TokenSecret))
            yield return $"{K("token_secret")} is empty; name the catalog secret of the AppProject role token, because every "
                + "PoC rollout is read with it";
        if (string.IsNullOrWhiteSpace(Project))
            yield return $"{K("project")} is empty; name the AppProject the PoC Applications belong to, because its role "
                + "token reads them";
        if (string.IsNullOrWhiteSpace(Namespace))
            yield return $"{K("namespace")} is empty; name the namespace Argo CD runs in, because the ApplicationSet lives there";
        if (!IsHttps(DestinationServer))
            yield return $"{K("destination_server")} is '{DestinationServer}'; it must be the https URL Argo CD names the "
                + "target cluster by, because every PoC Application is deployed there";
        if (string.IsNullOrWhiteSpace(RepoUrl))
            yield return $"{K("repo_url")} is empty; name the source repo as Argo CD's repository credential names it, "
                + "because the ApplicationSet's generator reads the deploy folders from it";
        if (TokenWarnDays < 0)
            yield return $"{K("token_warn_days")} is {TokenWarnDays}; it must be 0 or more, because a role token expires "
                + "silently and every PoC run then fails; two weeks leaves time to mint and store a new one";
        if (GeneratorRequeueSeconds is < 10 or > 180)
            yield return $"{K("generator_requeue_seconds")} is {GeneratorRequeueSeconds}; it must be 10 to 180, because the "
                + "git generator creates and deletes Applications only when it requeues; its default 180 s would spend a "
                + "third of the rollout deadline before a first deploy or a delete is seen, and below 10 it would poll the "
                + "repository host without pause";
    }

    private static bool IsHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;
}
