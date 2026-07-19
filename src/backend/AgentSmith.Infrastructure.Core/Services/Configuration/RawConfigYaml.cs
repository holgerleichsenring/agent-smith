using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0349: raw agentsmith.yml (de)serialization WITHOUT secret resolution — the
/// shape every config store and the import/export CLI share. Deserialize mirrors
/// the loader's converters but keeps env-NAME references intact; Serialize is the
/// GitOps/DR export that round-trips through the real loader.
/// </summary>
public static class RawConfigYaml
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new RawRepoRefYamlConverter())
        .IgnoreUnmatchedProperties()
        .Build();

    public static RawAgentSmithConfig Deserialize(string yaml) =>
        Deserializer.Deserialize<RawAgentSmithConfig>(yaml) ?? new RawAgentSmithConfig();

    public static string Serialize(RawAgentSmithConfig config) => ConfigYamlExporter.Export(config);
}
