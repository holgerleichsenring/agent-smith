using AgentSmith.Contracts.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    // YAML config-file loading helpers. Used by AddSecurityScanners for nuclei.yaml +
    // zap.yaml. Search order: AGENTSMITH_CONFIG_DIR (if set) > ./config/<file> > ./<file>
    // > AppContext.BaseDirectory/config/<file>. Missing file → default-constructed T.
    private static NucleiConfig LoadNucleiConfig() =>
        LoadYamlConfig<NucleiConfig>("nuclei.yaml");

    private static ZapConfig LoadZapConfig() =>
        LoadYamlConfig<ZapConfig>("zap.yaml");

    private static T LoadYamlConfig<T>(string fileName) where T : new()
    {
        var path = FindConfigFile(fileName);
        if (path is null) return new T();

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<T>(yaml) ?? new T();
    }

    internal static string? FindConfigFile(string fileName)
    {
        var candidates = new List<string>
        {
            Path.Combine("config", fileName),
            fileName,
            Path.Combine(AppContext.BaseDirectory, "config", fileName),
        };

        var configDir = Environment.GetEnvironmentVariable("AGENTSMITH_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
        {
            candidates.Insert(0, Path.Combine(configDir, fileName));
            candidates.Insert(1, Path.Combine(configDir, "config", fileName));
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
