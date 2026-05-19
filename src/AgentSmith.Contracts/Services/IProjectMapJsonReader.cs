using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Parses the LLM's terminal JSON response from <c>ProjectAnalyzer</c> into a
/// typed <see cref="ProjectMap"/>. Owns fence stripping, lenient parsing
/// (trailing commas, comments), the field-by-field mapping, and a single shared
/// error shape. Splits the JSON-decoding responsibility out of the LLM-driving
/// orchestrator so each class has one job.
/// </summary>
public interface IProjectMapJsonReader
{
    /// <summary>
    /// Attempts to parse <paramref name="finalText"/> into a <see cref="ProjectMap"/>.
    /// Returns <c>true</c> with a populated <paramref name="map"/> on success.
    /// On failure, returns <c>false</c> with a human-readable
    /// <paramref name="error"/> suitable for an LLM retry-prompt suffix.
    /// </summary>
    bool TryRead(string finalText, out ProjectMap? map, out string error);
}
