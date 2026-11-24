using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>2026-11-23-933dc: one match of one pattern on one line — where it starts and how long it is, so a caller
/// can mask it from the line itself rather than from a shortened copy of the value.</summary>
public sealed record PatternLineMatch(CompiledPattern Pattern, int Index, int Length, string Value);
