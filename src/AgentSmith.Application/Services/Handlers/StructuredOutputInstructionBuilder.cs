using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds LLM output format instructions based on the skill's orchestration role.
/// </summary>
public sealed class StructuredOutputInstructionBuilder
{
    public string Build(SkillOrchestration orchestration)
    {
        return orchestration.Role switch
        {
            SkillRole.Contributor => ContributorInstruction,
            SkillRole.Gate when orchestration.Output == SkillOutputType.List => GateListInstruction,
            SkillRole.Gate when orchestration.Output == SkillOutputType.Verdict => GateVerdictInstruction,
            SkillRole.Lead => "Synthesize the findings into a structured assessment. Respond with a clear, numbered summary.",
            SkillRole.Executor => "Based on the plan/assessment, produce your output.",
            _ => "Respond concisely."
        };
    }

    private const string ContributorInstruction =
        "Respond with a JSON array of findings. Each finding: " +
        "{ \"file\": \"\", \"line\": 0, \"title\": \"\", \"severity\": \"\", \"details\": \"\", " +
        "\"apiPath\": \"METHOD /path\", \"schemaName\": \"SchemaName\" }. " +
        "Use apiPath for endpoint-level findings and schemaName for schema-level findings. " +
        "Omit both for file-based findings. Max 50 items.";

    private const string GateListInstruction =
        "Review all findings. Respond with JSON: { \"confirmed\": [...], \"rejected\": [...] }. " +
        "Each item: { \"file\": \"\", \"line\": 0, \"title\": \"\", \"severity\": \"\", \"reason\": \"\", " +
        "\"apiPath\": \"METHOD /path\", \"schemaName\": \"SchemaName\" }. " +
        "Preserve apiPath/schemaName from contributor findings when present.";

    private const string GateVerdictInstruction =
        "Review the analysis. Respond with JSON: { \"pass\": true/false, \"reason\": \"\" }.";
}
