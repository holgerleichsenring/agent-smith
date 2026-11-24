namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dg: the Kubernetes namespace a PoC runs in — 'poc-' + its name (a DNS label of at most 40), so it fits
/// 63. The one source every render and the seal's strict-scope label use.
/// </summary>
public static class PocNamespace
{
    public const string Prefix = "poc-";

    public static string For(string pocName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pocName);
        return Prefix + pocName;
    }
}
