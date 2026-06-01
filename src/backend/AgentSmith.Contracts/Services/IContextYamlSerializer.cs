using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Single source of truth for context.yaml YAML emission and consumption.
/// Both directions share one YamlDotNet builder configuration — round-trip
/// is enforced by construction (p0193).
/// </summary>
public interface IContextYamlSerializer
{
    /// <summary>Emit YAML for a typed document. Quoting rules are YamlDotNet's defaults; LLM never types raw YAML.</summary>
    string Serialize(ContextYamlDocument document);

    /// <summary>Parse YAML into a structured result. Same semantics as the legacy ContextYamlParser.Parse.</summary>
    ContextYamlParseResult Parse(string yaml);
}
