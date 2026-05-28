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
        var chat = chatClientFactory.Create(request.AgentConfig, request.TaskType);
        var maxTokens = request.MaxOutputTokensOverride
            ?? chatClientFactory.GetMaxOutputTokens(request.AgentConfig, request.TaskType);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, request.SystemPrompt),
            new(ChatRole.User, request.UserPrompt),
        };
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
