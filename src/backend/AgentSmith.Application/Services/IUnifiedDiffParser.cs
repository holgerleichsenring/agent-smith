using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0167a: parses one file's unified-diff patch text into structured hunks with
/// resolved old/new line numbers. A pure format parser — no language or code
/// semantics (those belong to the LLM review skills).
/// </summary>
public interface IUnifiedDiffParser
{
    IReadOnlyList<PrHunk> Parse(string patch);
}
