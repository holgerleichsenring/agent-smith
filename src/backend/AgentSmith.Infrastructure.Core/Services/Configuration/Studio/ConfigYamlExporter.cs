using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// Serializes a <see cref="RawAgentSmithConfig"/> back to agentsmith.yml using the
/// exact naming + enum conventions and repo-ref converter the loader deserializes
/// with, so the output round-trips through <c>YamlConfigurationLoader</c>. This is
/// the "config as an OUTPUT" half of the storage-agnostic model: the canonical
/// document is the source of truth, the file a declarative export for GitOps and
/// disaster recovery.
/// </summary>
internal static class ConfigYamlExporter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new RawRepoRefYamlConverter())
        .ConfigureDefaultValuesHandling(
            DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .Build();

    public static string Export(RawAgentSmithConfig document) => Serializer.Serialize(document);
}
