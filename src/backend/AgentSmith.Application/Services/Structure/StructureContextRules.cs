using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>One context's judging terms as they stood at the branch's base.</summary>
public sealed record StructureContextRules(
    string Workdir, StructureLimitSet Limits, IReadOnlyList<StructureBaselineEntry> Baseline);
