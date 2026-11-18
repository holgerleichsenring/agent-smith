using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: reads a template's deployment.yaml into a <see cref="DeploymentDeclaration"/>, or says which key
/// is wrong. Keys the run does not act on (files, secrets) are left to the coding master. 2026-11-18-3863e: delivery
/// pipeline_trigger, and the parameters whose names a pipeline's inputs may use.
/// </summary>
public sealed class DeploymentDeclarationParser
{
    private static readonly string[] Deliveries = [DeploymentDeclaration.GitOpsPullRequest, DeploymentDeclaration.PipelineTrigger];

    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>The declaration, or null with the reason.</summary>
    public (DeploymentDeclaration? Declaration, string? Error) Parse(string text)
    {
        RawDeploymentDeclaration? raw;
        try { raw = Yaml.Deserialize<RawDeploymentDeclaration>(text); }
        catch (YamlException ex) { return (null, $"it is not readable YAML ({ex.Start}: {ex.Message})"); }
        if (raw is null) return (null, "it is empty");
        var delivery = string.IsNullOrWhiteSpace(raw.Delivery) ? DeploymentDeclaration.GitOpsPullRequest : raw.Delivery.Trim().ToLowerInvariant();
        if (!Deliveries.Contains(delivery))
            return (null, $"delivery '{delivery}' is not one agent-smith delivers by; {string.Join(" and ", Deliveries.Select(d => $"'{d}'"))} are");
        var parameters = (raw.Parameters ?? []).Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .ToDictionary(p => p.Name!.Trim(), p => p.Default, StringComparer.Ordinal);
        var environments = new List<DeclaredEnvironment>();
        foreach (var (environment, index) in (raw.Environments ?? []).Select((e, i) => (e, i)))
        {
            var (declared, error) = DeclaredEnvironmentReader.Read(environment, index, delivery, parameters.Keys);
            if (declared is null) return (null, error);
            environments.Add(declared);
        }
        if (PromotionChains.Cycle(environments) is { } cycle) return (null, cycle); // 2026-11-18-3863h
        return (new DeploymentDeclaration(delivery, environments, parameters), null);
    }
}
