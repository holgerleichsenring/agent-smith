using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Composes the "Project Brief" block for skill prompts from the loaded
/// .agentsmith/ artifacts (context.yaml + code-map.yaml + coding-principles.md).
/// Filters out sections irrelevant to code review (state.done, behavior.pipelines,
/// integrations) and returns a fallback brief when nothing was loaded.
/// </summary>
public interface IProjectBriefBuilder
{
    string Build(PipelineContext pipeline);
}
