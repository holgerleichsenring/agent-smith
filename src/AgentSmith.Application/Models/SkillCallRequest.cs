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
}
