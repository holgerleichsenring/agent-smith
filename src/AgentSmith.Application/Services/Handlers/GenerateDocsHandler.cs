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
/// Generates or updates documentation for code changes via the agentic LLM loop.
/// Runs after Test in the add-feature pipeline.
/// </summary>
public sealed class GenerateDocsHandler(
    IChatClientFactory chatClientFactory,
    AgentPromptBuilder promptBuilder,
    IDecisionLogger decisionLogger,
    IDialogueTransport? dialogueTransport,
    ILogger<GenerateDocsHandler> logger)
    : ICommandHandler<GenerateDocsContext>
{

    public async Task<CommandResult> ExecuteAsync(
        GenerateDocsContext context, CancellationToken cancellationToken)
    {
        if (context.Changes.Count == 0)
        {
            logger.LogInformation("No code changes to generate docs for, skipping");
            return CommandResult.Ok("No code changes, skipping doc generation");
        }

        var changedFiles = string.Join(", ", context.Changes.Select(c => c.Path.Value));
        logger.LogInformation("Generating docs for {Count} changed files: {Files}",
            context.Changes.Count, changedFiles);

        var plan = BuildSyntheticPlan(context.Changes);
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);

        var toolHost = new SandboxToolHost(
            sandbox, decisionLogger, dialogueTransport, jobId: null, context.Repository.LocalPath);

        var systemPrompt = promptBuilder.BuildExecutionSystemPrompt(
            context.CodingPrinciples, context.CodeMap, context.ProjectContext);
        var userPrompt = promptBuilder.BuildExecutionUserPrompt(plan, context.Repository);

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

        var response = await chat.GetResponseAsync(messages, options, cancellationToken);
        PipelineCostTracker.GetOrCreate(context.Pipeline).Track(response);

        MergeCodeChanges(context, toolHost.GetChanges());

        logger.LogInformation("Doc generation completed: {Count} files changed", toolHost.GetChanges().Count);
        return CommandResult.Ok($"Generated docs: {toolHost.GetChanges().Count} files changed");
    }

    private static Plan BuildSyntheticPlan(IReadOnlyList<CodeChange> changes)
    {
        var fileList = string.Join("\n", changes.Select(c => $"- {c.Path.Value} ({c.ChangeType})"));

        var description =
            $"""
             Update or generate documentation for the following code changes.
             - If a README exists and new features/endpoints/commands were added, update it
             - Add or update inline XML/JSDoc/docstring comments for new public types and methods
             - If a CHANGELOG or API docs file exists, add an entry for the changes
             - Do NOT create documentation files that don't follow existing repo conventions

             Changed files:
             {fileList}
             """;

        var step = new PlanStep(1, description, null, "modify");
        return new Plan("Update documentation for code changes", [step], description);
    }

    private static void MergeCodeChanges(GenerateDocsContext context, IReadOnlyList<CodeChange> newChanges)
    {
        if (newChanges.Count == 0)
            return;

        var existing = context.Pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        var merged = existing.Concat(newChanges).ToList();
        context.Pipeline.Set(ContextKeys.CodeChanges, (IReadOnlyList<CodeChange>)merged);
    }
}
