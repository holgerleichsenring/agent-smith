using System.Diagnostics;
using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Application.Services.Prompts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Executes the plan via Microsoft.Extensions.AI: resolves an IChatClient with
/// FunctionInvokingChatClient (wrapped by IChatClientFactory for tool-bearing tasks),
/// builds the SandboxToolHost tool surface, runs GetResponseAsync, then collects
/// changes/decisions from the SandboxToolHost.
/// </summary>
public sealed class AgenticExecuteHandler(
    IChatClientFactory chatClientFactory,
    AgentPromptBuilder promptBuilder,
    IDecisionLogger decisionLogger,
    IDialogueTransport? dialogueTransport,
    ILogger<AgenticExecuteHandler> logger)
    : ICommandHandler<AgenticExecuteContext>
{

    public async Task<CommandResult> ExecuteAsync(
        AgenticExecuteContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing plan with {Steps} steps...", context.Plan.Steps.Count);

        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var toolHost = new SandboxToolHost(
            sandbox, decisionLogger, dialogueTransport, jobId: null, context.Repository.LocalPath);

        var systemPrompt = promptBuilder.BuildExecutionSystemPrompt(
            context.CodingPrinciples, context.CodeMap, context.ProjectContext);
        var userPrompt = promptBuilder.BuildExecutionUserPrompt(context.Plan, context.Repository);

        var chat = chatClientFactory.Create(context.AgentConfig, TaskType.Primary);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.Primary);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };
        var options = new ChatOptions
        {
            Tools = toolHost.GetAllTools(),
            MaxOutputTokens = maxTokens,
        };

        var sw = Stopwatch.StartNew();
        var response = await chat.GetResponseAsync(messages, options, cancellationToken);
        sw.Stop();

        var costTracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        costTracker.Track(response);

        var changes = toolHost.GetChanges();
        var decisions = toolHost.GetDecisions();

        context.Pipeline.Set(ContextKeys.CodeChanges, changes);
        context.Pipeline.Set(ContextKeys.RunDurationSeconds, (int)sw.Elapsed.TotalSeconds);

        if (decisions.Count > 0)
        {
            context.Pipeline.AppendDecisions(decisions);
        }

        logger.LogInformation(
            "Agentic execution completed: {Count} files changed, {Decisions} decisions",
            changes.Count, decisions.Count);

        return CommandResult.Ok($"Agentic execution completed: {changes.Count} files changed");
    }
}
