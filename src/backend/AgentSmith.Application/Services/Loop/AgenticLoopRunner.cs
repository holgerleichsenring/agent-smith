using System.Diagnostics;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: shared agentic loop core. Builds the chat client via the factory,
/// composes the message list, opens the CallScope so emitted events carry
/// role + phase + repo, runs <c>chat.GetResponseAsync</c> with the
/// caller-supplied tool surface, and returns the response.
///
/// <para>Cost tracking + post-call collection (changes, decisions) stay
/// with the caller — the master handler and the sub-agent runner both
/// own different collection paths. This service does not know whether the
/// caller is a master or a child; that decision lives in the request's
/// identity tuple.</para>
/// </summary>
public sealed class AgenticLoopRunner(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ILogger<AgenticLoopRunner> logger) : IAgenticLoopRunner
{
    public async Task<AgenticLoopResult> RunAsync(
        AgenticLoopRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // p0341c: thread the per-pass iteration ceiling (the master's large anti-runaway
        // safety net / a sub-agent's real child budget) and the open-loop governor hooks
        // (within-pass budget fence + ledger-reminder injection) into the chat client.
        // 2026-09-01-7df4: the compaction settings and the assumed input window travel with
        // the request, so a surface that raises its ceiling can also reduce what it holds.
        var chat = chatClientFactory.Create(
            request.AgentConfig, request.TaskType, request.MaxIterations, request.MasterLoopHooks,
            request.Compaction, request.ContextWindowTokensOverride);
        var maxTokens = request.MaxOutputTokensOverride
            ?? chatClientFactory.GetMaxOutputTokens(request.AgentConfig, request.TaskType);
        // p0317: ticket images ride the user message as image content parts
        // (text first, then the images) — only set for vision-capable models.
        var userMessage = LoopUserMessage.Compose(request.UserPrompt, request.UserImageParts);
        // p0341f: system prompt, then the conversation this call continues, then the new
        // turn. The order is the cache order: the stable prefix first, the growing
        // transcript next, the only new text last — so a re-engaged pass pays cache-read
        // price for what it already knows instead of full price to learn it again.
        var messages = new List<ChatMessage> { new(ChatRole.System, request.SystemPrompt) };
        if (request.PriorMessages is { Count: > 0 } prior) messages.AddRange(prior);
        messages.Add(userMessage);
        var options = new ChatOptions
        {
            Tools = request.Tools,
            MaxOutputTokens = maxTokens,
        };

        var role = request.Name ?? "agentic-executor";
        var phase = SkillExecutionPhase.Implementation.ToString();
        var sw = Stopwatch.StartNew();
        using var scope = runContext.BeginCallScope(role, phase);
        logger.LogDebug(
            "AgenticLoopRunner.RunAsync — role={Role} subAgentId={SubAgentId} parent={ParentSubAgentId}",
            role, request.SubAgentId, request.ParentSubAgentId);
        var response = await chat.GetResponseAsync(messages, options, cancellationToken);
        sw.Stop();
        return new AgenticLoopResult(response, sw.Elapsed);
    }
}
