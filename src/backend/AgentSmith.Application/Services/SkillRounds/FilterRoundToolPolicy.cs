using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// Filter-round tool policy: always empty. Filter skills decide inclusion
/// based on the observations they were given + their declared role; fetching
/// more code mid-filter changes the contract from "judge what you got" to
/// "investigate further." Per-batch dispatch under p0124's batching also
/// makes tool budget hard to reason about across batches.
/// </summary>
public sealed class FilterRoundToolPolicy : ISkillRoundToolPolicy
{
    public IReadOnlyList<AITool> GetTools(RoleSkillDefinition role, PipelineContext pipeline) => [];
}
