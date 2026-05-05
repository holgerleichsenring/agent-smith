using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Generates unit tests for code changes via the agentic LLM loop.
/// Runs between AgenticExecute and Test in the add-feature pipeline.
/// </summary>
public sealed class GenerateTestsHandler(
    IAgentProviderFactory factory,
    IProgressReporter progressReporter,
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

        var changedFiles = string.Join(", ", context.Changes.Select(c => c.Path.Value));
        logger.LogInformation("Generating tests for {Count} changed files: {Files}",
            context.Changes.Count, changedFiles);

        var plan = BuildSyntheticPlan(context.Changes);
        var provider = factory.Create(context.AgentConfig);

        var result = await provider.ExecutePlanAsync(
            plan, context.Repository, context.CodingPrinciples,
            context.CodeMap, context.ProjectContext, progressReporter,
            sandbox: null, cancellationToken);

        MergeCodeChanges(context, result.Changes);

        logger.LogInformation("Test generation completed: {Count} files changed", result.Changes.Count);
        return CommandResult.Ok($"Generated tests: {result.Changes.Count} files changed");
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

    private static void MergeCodeChanges(GenerateTestsContext context, IReadOnlyList<CodeChange> newChanges)
    {
        if (newChanges.Count == 0)
            return;

        var existing = context.Pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        var merged = existing.Concat(newChanges).ToList();
        context.Pipeline.Set(ContextKeys.CodeChanges, (IReadOnlyList<CodeChange>)merged);
    }
}
