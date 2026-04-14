using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Builds upstream context for structured skill rounds based on the skill's orchestration role.
/// </summary>
public interface IUpstreamContextBuilder
{
    string Build(
        SkillRole role,
        PipelineContext pipeline,
        Dictionary<string, string> skillOutputs);
}
