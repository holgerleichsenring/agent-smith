using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Parses LLM gate output (verdict or list) and writes confirmed findings to the pipeline.
/// </summary>
public interface IGateOutputHandler
{
    CommandResult Handle(
        RoleSkillDefinition role,
        SkillOrchestration orchestration,
        string responseText,
        PipelineContext pipeline);
}
