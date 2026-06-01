using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// p0193: thin adapter that delegates to <see cref="IContextYamlSerializer"/>.
/// The parser exists only to keep the IContextYamlParser contract stable for
/// existing callers (SandboxLanguageResolver, ProjectMetaResolver). All
/// real YAML logic lives in ContextYamlSerializer so that emit and consume
/// share one builder.
/// </summary>
public sealed class ContextYamlParser(IContextYamlSerializer serializer) : IContextYamlParser
{
    public ContextYamlParseResult Parse(string yaml) => serializer.Parse(yaml);
}
