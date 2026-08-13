using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// The single YamlDotNet builder configuration for every context.yaml
/// emit + consume path (the p0193 one-builder rule). Injected into
/// <see cref="ContextYamlSerializer"/> and
/// <see cref="ContextYamlRegistryAuthCodec"/> so a document written by one
/// is parseable by the other by construction — p0401: one instance from the
/// container rather than one instance per process.
/// </summary>
public sealed class ContextYamlBuilders
{
    public ISerializer Serializer { get; } = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public IDeserializer Deserializer { get; } = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
}
