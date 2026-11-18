namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-18-3863a: what a template binding is for. A code template answers how a context is BUILT; a deployment
/// template answers how it is DELIVERED. Absent on a binding means code — every binding written before the kind
/// existed.
/// </summary>
public static class TemplateKinds
{
    public const string Code = "code";

    public const string Deployment = "deployment";

    /// <summary>Whether <paramref name="kind"/> names a kind; null and blank are code.</summary>
    public static bool IsKnown(string? kind) =>
        string.IsNullOrWhiteSpace(kind) || IsDeployment(kind) || string.Equals(kind, Code, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="kind"/> is the deployment kind.</summary>
    public static bool IsDeployment(string? kind) => string.Equals(kind, Deployment, StringComparison.OrdinalIgnoreCase);
}
