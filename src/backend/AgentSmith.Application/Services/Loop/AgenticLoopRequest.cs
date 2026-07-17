using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: input record for <see cref="IAgenticLoopRunner"/>. Carries the
/// fully-built prompt + tool surface for one agentic loop invocation,
/// plus the identity tuple (Name, Activity, SubAgentId, ParentSubAgentId)
/// that flows into the per-call CallScope so emitted events are attributable.
///
/// <para>InheritedContext is data only — the master agent passes a
/// snapshot (PipelineGoal, PriorContextSlice, OptionalSystemPromptBlock)
/// into each child; the cache architecture is deferred to p0178.</para>
/// </summary>
public sealed record AgenticLoopRequest(
    AgentConfig AgentConfig,
    TaskType TaskType,
    string SystemPrompt,
    string UserPrompt,
    IList<AITool> Tools,
    int? MaxOutputTokensOverride = null,
    InheritedContext? InheritedContext = null,
    string? Name = null,
    string? Activity = null,
    string? SubAgentId = null,
    string? ParentSubAgentId = null,
    // p0317: ticket image attachments as image content parts, appended to the
    // user message after the prompt text. Empty/null keeps the text-only path.
    IReadOnlyList<AIContent>? UserImageParts = null,
    // p0341c: the per-pass tool-iteration ceiling for THIS loop — the anti-runaway
    // SAFETY net, not the stopping control (money + verification are). Null inherits
    // ChatClientFactory's legacy 25 default; the master passes its large ceiling and a
    // sub-agent its own real child budget.
    int? MaxIterations = null,
    // p0341c: the open-loop governor hooks (within-pass budget fence + ledger reminder).
    // Non-null only for the coding master's Primary calls; null keeps the plain chain.
    MasterLoopHooks? MasterLoopHooks = null);
