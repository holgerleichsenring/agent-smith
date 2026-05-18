using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Loads agentsmith.yml: deserializes into a raw shape, resolves environment
/// variable placeholders in secrets, normalizes per-project pipeline declarations
/// (legacy single-pipeline shim + default-pipeline validation), then materializes
/// catalog references via <see cref="ConfigCatalogResolver"/>.
/// Also fills <see cref="SkillsConfig.CacheDir"/> from
/// <see cref="IAgentSmithPaths.SkillsCatalogRoot"/> when the operator left it blank.
/// </summary>
public sealed class YamlConfigurationLoader(
    ProjectConfigNormalizer normalizer,
    ConfigCatalogResolver resolver,
    IAgentSmithPaths paths) : IConfigurationLoader
{
    public AgentSmithConfig LoadConfig(string configPath)
    {
        var yaml = ReadFile(configPath);
        var raw = Deserialize(yaml, configPath);
        ResolveSecrets(raw);
        NormalizeProjects(raw);
        FillSkillsDefaults(raw);
        return resolver.Resolve(raw);
    }

    private void NormalizeProjects(RawAgentSmithConfig raw)
    {
        foreach (var (name, project) in raw.Projects)
            normalizer.Normalize(name, project);
    }

    private void FillSkillsDefaults(RawAgentSmithConfig raw)
    {
        if (string.IsNullOrWhiteSpace(raw.Skills.CacheDir))
            raw.Skills.CacheDir = paths.SkillsCatalogRoot;
    }

    private static string ReadFile(string configPath)
    {
        if (!File.Exists(configPath))
            throw new ConfigurationException($"Configuration file not found: {configPath}");

        return File.ReadAllText(configPath);
    }

    private static RawAgentSmithConfig Deserialize(string yaml, string configPath)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            return deserializer.Deserialize<RawAgentSmithConfig>(yaml)
                   ?? throw new ConfigurationException("Configuration file is empty.");
        }
        catch (Exception ex) when (ex is not ConfigurationException)
        {
            throw new ConfigurationException($"Invalid YAML in {configPath}: {ex.Message}");
        }
    }

    private static void ResolveSecrets(RawAgentSmithConfig raw)
    {
        var resolved = new Dictionary<string, string>();

        foreach (var (key, value) in raw.Secrets)
            resolved[key] = ResolveEnvironmentVariable(value);

        raw.Secrets = resolved;
    }

    private static string ResolveEnvironmentVariable(string value)
    {
        if (!value.StartsWith("${") || !value.EndsWith("}"))
            return value;

        var varName = value[2..^1];
        return Environment.GetEnvironmentVariable(varName) ?? string.Empty;
    }
}
