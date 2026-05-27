using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Parses a `.agentsmith/contexts/&lt;name&gt;/context.yaml` string into the
/// minimal summary the orchestrator needs (workdir + language). Implementation
/// uses YamlDotNet — lives in Infrastructure so Contracts stays YAML-library
/// independent.
/// </summary>
public interface IContextYamlParser
{
    /// <summary>
    /// Parses the YAML content and returns the summary, or null when the YAML
    /// is empty / malformed / does not match the expected shape. Throws
    /// InvalidOperationException when meta.workdir is missing — per p0161
    /// absence is always misconfiguration, never a silent default.
    /// </summary>
    ContextYamlSummary? TryParse(string yaml);
}
