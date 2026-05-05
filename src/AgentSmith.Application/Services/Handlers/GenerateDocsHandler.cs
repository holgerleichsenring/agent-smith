using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Generates or updates documentation for code changes via the agentic LLM loop.
/// Runs after Test in the add-feature pipeline.
/// </summary>
public sealed class GenerateDocsHandler(
    IAgentProviderFactory factory,
    IProgressReporter progressReporter,
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
        var provider = factory.Create(context.AgentConfig);

        var result = await provider.ExecutePlanAsync(
            plan, context.Repository, context.CodingPrinciples,
            context.CodeMap, context.ProjectContext, progressReporter,
            sandbox: null, cancellationToken);

        MergeCodeChanges(context, result.Changes);

        logger.LogInformation("Doc generation completed: {Count} files changed", result.Changes.Count);
        return CommandResult.Ok($"Generated docs: {result.Changes.Count} files changed");
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
