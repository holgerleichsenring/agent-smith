using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Models;

/// <summary>
/// Input contract for <c>ISkillCallRuntime.ExecuteAsync</c>. Carries the skill identity,
/// per-call phase + investigator-mode for tool-set selection and limit resolution,
/// the prompt parts, the tool-set the LLM may call, and the per-call agent + task
/// configuration the chat-client factory needs.
/// </summary>
public sealed record SkillCallRequest
{
    public required string SkillName { get; init; }
    public required string Role { get; init; }
    public required SkillExecutionPhase Phase { get; init; }
    public string? InvestigatorMode { get; init; }
    public required IReadOnlyList<ChatMessage> PromptParts { get; init; }
    public required IReadOnlyList<AITool> ToolSet { get; init; }
    public required AgentConfig AgentConfig { get; init; }
    public required TaskType TaskType { get; init; }

    /// <summary>
    /// p0145: pipeline preset name (e.g. "fix-bug", "schedule-appointment").
    /// Carried for the future <c>SkillCallRuntime</c> → <c>IToolKit</c>
    /// integration in p0142, which will use it to compose <c>ToolSet</c>
    /// internally. Null on legacy callers; consumed via the
    /// <see cref="Services.Tools.IToolKit.WildcardPipelineName"/> sentinel.
    /// </summary>
    public string? PipelineName { get; init; }

    /// <summary>
    /// Declared output schema (observation/plan/diff/bootstrap) from RoleSkillDefinition.
    /// Null on legacy paths that have not migrated to the new SKILL.md format yet —
    /// SkillOutputValidatorFactory falls back to NoOpSkillOutputValidator in that case.
    /// </summary>
    public string? OutputSchema { get; init; }
}
