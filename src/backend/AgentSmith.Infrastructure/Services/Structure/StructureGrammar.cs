using TreeSitter;

namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: a loaded grammar and its compiled query, shared by every measurement of
/// its language. Both are read-only once built; parsers and cursors are made per call.
/// <paramref name="Failure"/> says why a grammar could not be loaded — then both are null.
/// </summary>
public sealed record StructureGrammar(Language? Language, Query? Query, string? Failure)
{
    public static StructureGrammar Failed(string reason) => new(null, null, reason);
}
