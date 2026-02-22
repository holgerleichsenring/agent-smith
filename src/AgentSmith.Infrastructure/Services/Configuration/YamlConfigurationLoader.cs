using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Services.Configuration;

/// <summary>
/// Loads configuration from a YAML file and resolves environment variable placeholders.
/// </summary>
public sealed class YamlConfigurationLoader : IConfigurationLoader
{
    public AgentSmithConfig LoadConfig(string configPath)
    {
        var yaml = ReadFile(configPath);
        var config = Deserialize(yaml, configPath);
        ResolveSecrets(config);
        return config;
    }

    private static string ReadFile(string configPath)
    {
        if (!File.Exists(configPath))
            throw new ConfigurationException($"Configuration file not found: {configPath}");

        return File.ReadAllText(configPath);
    }

    private static AgentSmithConfig Deserialize(string yaml, string configPath)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            return deserializer.Deserialize<AgentSmithConfig>(yaml)
                   ?? throw new ConfigurationException("Configuration file is empty.");
        }
        catch (Exception ex) when (ex is not ConfigurationException)
        {
            throw new ConfigurationException($"Invalid YAML in {configPath}: {ex.Message}");
        }
    }

    private static void ResolveSecrets(AgentSmithConfig config)
    {
        var resolved = new Dictionary<string, string>();

        foreach (var (key, value) in config.Secrets)
            resolved[key] = ResolveEnvironmentVariable(value);

        config.Secrets = resolved;
    }

    private static string ResolveEnvironmentVariable(string value)
    {
        if (!value.StartsWith("${") || !value.EndsWith("}"))
            return value;

        var varName = value[2..^1];
        return Environment.GetEnvironmentVariable(varName) ?? string.Empty;
    }
}
