using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Triages based on code analysis (file structure, frameworks, dependencies)
/// to determine which security specialist roles should participate.
/// Used by the security-scan pipeline.
/// </summary>
public sealed class SecurityTriageHandler(
    ILlmClientFactory llmClientFactory,
    ISkillGraphBuilder skillGraphBuilder,
    IPromptCatalog prompts,
    ILogger<SecurityTriageHandler> logger)
    : TriageHandlerBase, ICommandHandler<SecurityTriageContext>
{
    protected override ILogger Logger => logger;
    protected override IPromptCatalog Prompts => prompts;
    protected override string SkillRoundCommandName => "SecuritySkillRoundCommand";
    protected override ISkillGraphBuilder? GraphBuilder => skillGraphBuilder;

    protected override string BuildUserPrompt(PipelineContext pipeline)
    {
        pipeline.TryGet<CodeAnalysis>(ContextKeys.CodeAnalysis, out var codeAnalysis);
        pipeline.TryGet<string>(ContextKeys.CodingPrinciples, out var principles);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);

        var filesSummary = codeAnalysis is not null
            ? $"Language: {codeAnalysis.Language}, Framework: {codeAnalysis.Framework}\n" +
              $"Files: {codeAnalysis.FileStructure.Count} total\n" +
              $"Dependencies: {string.Join(", ", codeAnalysis.Dependencies.Take(20))}"
            : "Code analysis not available";

        // Code-area signals for skill selection
        var signalAnalysis = "";
        if (codeAnalysis is not null)
        {
            var files = codeAnalysis.FileStructure;
            var fileNames = files.Select(f => f.ToLowerInvariant()).ToList();

            var hasDirectObjectReferences = fileNames.Any(f =>
                f.Contains("controller") || f.Contains("handler") || f.Contains("endpoint"));
            var hasInputEntryPoints = fileNames.Any(f =>
                f.Contains("request") || f.Contains("dto") || f.Contains("form") || f.Contains("input"));
            var hasAuthGuardChanges = fileNames.Any(f =>
                f.Contains("auth") || f.Contains("guard") || f.Contains("policy") || f.Contains("permission"));
            var hasErrorHandlingChanges = fileNames.Any(f =>
                f.Contains("exception") || f.Contains("error") || f.Contains("middleware"));

            signalAnalysis = $"""
                ## Code-Area Signals
                - Direct object references (controllers/handlers): {hasDirectObjectReferences}
                - Input entry points (DTOs/forms/requests): {hasInputEntryPoints}
                - Auth guard/policy changes: {hasAuthGuardChanges}
                - Error handling changes: {hasErrorHandlingChanges}
                """;
        }

        return $"""
            ## Security Scan Target
            {filesSummary}

            ## Project Context
            {projectContext ?? "Not available"}

            ## Coding Principles
            {principles ?? "Not available"}

            {signalAnalysis}

            Determine which security specialist roles should review this codebase.
            Consider the language, framework, dependencies, file structure, and signals above
            to select the most relevant security reviewers.
            The chain-analyst must always be selected as executor.
            """;
    }

    public async Task<CommandResult> ExecuteAsync(
        SecurityTriageContext context, CancellationToken cancellationToken)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);
        return await TriageAsync(context.Pipeline, llmClient, cancellationToken);
    }
}
