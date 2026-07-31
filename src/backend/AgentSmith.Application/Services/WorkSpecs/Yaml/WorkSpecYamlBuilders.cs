using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Application.Services.WorkSpecs.Yaml;

/// <summary>
/// p0390: the single YamlDotNet configuration for spec.yaml emit + consume, so a
/// spec this system writes parses back by construction — the p0193 one-builder
/// rule applied to the work spec.
/// </summary>
internal static class WorkSpecYamlBuilders
{
    public static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
}
