using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds LLM output format instructions based on the skill's orchestration role.
/// </summary>
public sealed class StructuredOutputInstructionBuilder(IPromptCatalog prompts)
{
    public string Build(SkillOrchestration orchestration)
    {
        return orchestration.Role switch
        {
            SkillRole.Contributor => prompts.Get("structured-output-contributor"),
            SkillRole.Gate when orchestration.Output == SkillOutputType.List
                => prompts.Get("structured-output-gate-list"),
            SkillRole.Gate when orchestration.Output == SkillOutputType.Verdict
                => prompts.Get("structured-output-gate-verdict"),
            SkillRole.Lead => "Synthesize the findings into a structured assessment. Respond with a clear, numbered summary.",
            SkillRole.Executor => "Based on the plan/assessment, produce your output.",
            _ => "Respond concisely."
        };
    }
}
