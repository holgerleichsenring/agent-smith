using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// The single YamlDotNet builder configuration for every context.yaml
/// emit + consume path (the p0193 one-builder rule). Shared by
/// <see cref="ContextYamlSerializer"/> and
/// <see cref="ContextYamlRegistryAuthCodec"/> so a document written by one
/// is parseable by the other by construction.
/// </summary>
internal static class ContextYamlBuilders
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
