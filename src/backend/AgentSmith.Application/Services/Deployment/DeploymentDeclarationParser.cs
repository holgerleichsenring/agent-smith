using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: reads a template's deployment.yaml into a <see cref="DeploymentDeclaration"/>, or says which key
/// is wrong. Keys the run does not act on (files, parameters, secrets) are left to the coding master.
/// </summary>
public sealed class DeploymentDeclarationParser
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>The declaration, or null with the reason.</summary>
    public (DeploymentDeclaration? Declaration, string? Error) Parse(string text)
    {
        RawDeclaration? raw;
        try { raw = Yaml.Deserialize<RawDeclaration>(text); }
        catch (YamlException ex) { return (null, $"it is not readable YAML ({ex.Start}: {ex.Message})"); }
        if (raw is null) return (null, "it is empty");
        var delivery = string.IsNullOrWhiteSpace(raw.Delivery) ? DeploymentDeclaration.GitOpsPullRequest : raw.Delivery.Trim();
        if (!string.Equals(delivery, DeploymentDeclaration.GitOpsPullRequest, StringComparison.OrdinalIgnoreCase))
            return (null, $"delivery '{delivery}' is not one this version delivers by; '{DeploymentDeclaration.GitOpsPullRequest}' is");
        var environments = new List<DeclaredEnvironment>();
        foreach (var (environment, index) in (raw.Environments ?? []).Select((e, i) => (e, i)))
        {
            if (Missing(environment, index) is { } missing) return (null, missing);
            environments.Add(new DeclaredEnvironment(environment.Name!.Trim(), environment.Manifest!.Trim(),
                environment.VersionKey!.Trim(), string.IsNullOrWhiteSpace(environment.PromotesFrom) ? null : environment.PromotesFrom.Trim()));
        }
        return (new DeploymentDeclaration(DeploymentDeclaration.GitOpsPullRequest, environments), null);
    }

    private static string? Missing(RawEnvironment environment, int index) =>
        string.IsNullOrWhiteSpace(environment.Name) ? $"environments[{index}] has no name"
        : string.IsNullOrWhiteSpace(environment.Manifest) ? $"environment '{environment.Name}' has no manifest"
        : string.IsNullOrWhiteSpace(environment.VersionKey) ? $"environment '{environment.Name}' has no version_key"
        : null;

    private sealed class RawDeclaration
    {
        public string? Delivery { get; set; }
        public List<RawEnvironment>? Environments { get; set; }
    }

    private sealed class RawEnvironment
    {
        public string? Name { get; set; }
        public string? Manifest { get; set; }
        public string? VersionKey { get; set; }
        public string? PromotesFrom { get; set; }
    }
}
