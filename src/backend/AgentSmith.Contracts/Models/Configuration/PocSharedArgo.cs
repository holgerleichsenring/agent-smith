namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-c81ec: two targets on one Argo CD (one argo.url) with the same argo.namespace or the same
/// destination_server would both claim the Application and namespace poc-&lt;name&gt;, because PoC names are unique only
/// per target — a blocking finding naming both targets and the keys, instead of a prefix that would break the
/// Application = namespace = PocNamespace naming.
/// </summary>
public sealed class PocSharedArgo(IReadOnlyList<PocTargetConfig> targets)
{
    public IEnumerable<string> Problems()
    {
        var argos = targets.Where(t => t.Argo is { Url.Length: > 0 }).ToList();
        foreach (var a in argos)
        foreach (var b in argos.Where(b => string.CompareOrdinal(a.Name, b.Name) < 0 && SameUrl(a.Argo!.Url, b.Argo!.Url)))
        {
            var key = Same(a.Argo!.Namespace, b.Argo!.Namespace) ? "namespace"
                : Same(a.Argo.DestinationServer, b.Argo.DestinationServer) ? "destination_server" : null;
            if (key is null) continue;
            yield return $"{PocArgoConfig.Key}.url of targets '{a.Name}' and '{b.Name}' is the one Argo CD '{a.Argo.Url}' with the "
                + $"same {PocArgoConfig.Key}.{key}; give each target its own Argo CD, namespace and destination_server, "
                + "because both ApplicationSets would claim the Application and namespace poc-<name>, and PoC names are unique "
                + "only per target";
        }
    }

    private static bool SameUrl(string a, string b) => Same(a.TrimEnd('/'), b.TrimEnd('/'));

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
