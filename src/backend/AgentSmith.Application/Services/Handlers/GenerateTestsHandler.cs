using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
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
/// Generates unit tests for code changes via the agentic LLM loop.
/// Runs between AgenticExecute and Test in the add-feature pipeline.
/// </summary>
public sealed class GenerateTestsHandler(
    IChatClientFactory chatClientFactory,
    AgentPromptBuilder promptBuilder,
    IDecisionLogger decisionLogger,
    IDialogueTransport? dialogueTransport,
    IRunContextAccessor runContext,
    RepoDiffPartitioner repoDiffPartitioner,
    ILogger<GenerateTestsHandler> logger)
    : ICommandHandler<GenerateTestsContext>
{

    public async Task<CommandResult> ExecuteAsync(
        GenerateTestsContext context, CancellationToken cancellationToken)
    {
        if (context.Changes.Count == 0)
        {
            logger.LogInformation("No code changes to generate tests for, skipping");
            return CommandResult.Ok("No code changes, skipping test generation");
        }

        // p0355: scope the pass to the repos that actually changed — an
        // unchanged repo gets no tests generated (compute saved, no drift onto
        // untouched code); the skip is surfaced in the step result.
        var partition = await repoDiffPartitioner.PartitionAsync(context.Pipeline, cancellationToken);
        if (partition.ChangedRepoNames.Count == 0)
        {
            logger.LogInformation("No repo has a working-tree diff, skipping test generation");
            return CommandResult.Ok("No diff in any repo — test generation skipped");
        }

        var changedFiles = string.Join(", ", context.Changes.Select(c => c.Path.Value));
        logger.LogInformation("Generating tests for {Count} changed files: {Files}",
            context.Changes.Count, changedFiles);

        var plan = BuildSyntheticPlan(context.Changes);
        var fs = new FilesystemToolHost(
            partition.ChangedSandboxes, partition.ChangedRepoNames[0], context.Repository.LocalPath);
        var log = new LogDecisionToolHost(decisionLogger, context.Repository.LocalPath);
        var human = new HumanToolHost(dialogueTransport);

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
            Tools = AgenticToolSurface.ReadWriteWithHuman(fs, log, human),
            MaxOutputTokens = maxTokens,
        };

        using var _scope = runContext.BeginCallScope(
            "tests-generator", SkillExecutionPhase.Implementation.ToString());
        var response = await chat.GetResponseAsync(messages, options, cancellationToken);
        PipelineCostTracker.GetOrCreate(context.Pipeline).Track(response);

        var changes = fs.GetChanges();
        MergeCodeChanges(context, changes);

        logger.LogInformation("Test generation completed: {Count} files changed", changes.Count);
        return CommandResult.Ok($"Generated tests: {changes.Count} files changed{SkipNote(partition)}");
    }

    private static Plan BuildSyntheticPlan(IReadOnlyList<CodeChange> changes)
    {
        var fileList = string.Join("\n", changes.Select(c => $"- {c.Path.Value} ({c.ChangeType})"));

        var description =
            $"""
             Generate unit tests for the following code changes. Analyze existing test patterns
             in the repository (test framework, naming conventions, folder structure) and follow them.
             Only test new or modified public API — do not test private internals.
             If no test project exists, create one following the project's framework conventions.

             Changed files:
             {fileList}
             """;

        var step = new PlanStep(1, description, null, "create");
        return new Plan("Generate unit tests for code changes", [step], description);
    }

    private static string SkipNote(RepoDiffPartition partition) =>
        partition.SkippedRepoNames.Count == 0
            ? string.Empty
            : $" (no diff in {string.Join(", ", partition.SkippedRepoNames)} — skipped)";

    private static void MergeCodeChanges(GenerateTestsContext context, IReadOnlyList<CodeChange> newChanges)
    {
        if (newChanges.Count == 0)
            return;

        var existing = context.Pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        var merged = existing.Concat(newChanges).ToList();
        context.Pipeline.Set(ContextKeys.CodeChanges, (IReadOnlyList<CodeChange>)merged);
    }
}
