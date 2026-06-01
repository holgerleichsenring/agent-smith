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
    /// Parses YAML content into a structured result. A scanner / parser
    /// error surfaces as <see cref="ContextYamlParseResult.ErrorReason"/>
    /// with line/col + the original YamlDotNet message — callers log it so
    /// operators see WHY (e.g. unquoted '@scope/pkg' at line 22 col 7)
    /// instead of a downstream "fell back to generic image" symptom.
    ///
    /// Still throws <see cref="System.InvalidOperationException"/> when the
    /// YAML parsed cleanly but meta.workdir is missing — per p0161 absence
    /// of workdir is always misconfiguration, never a silent default.
    /// </summary>
    ContextYamlParseResult Parse(string yaml);
}
