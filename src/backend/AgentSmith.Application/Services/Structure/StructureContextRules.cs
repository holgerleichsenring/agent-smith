using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>2026-10-06-8565d: one context's judging terms as they stood at the branch's base.</summary>
public sealed record StructureContextRules(
    string Workdir, StructureLimitSet Limits, IReadOnlyList<StructureBaselineEntry> Baseline);
