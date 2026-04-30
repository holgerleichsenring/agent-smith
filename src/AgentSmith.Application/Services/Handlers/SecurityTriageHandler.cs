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
        pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var projectMap);
        pipeline.TryGet<string>(ContextKeys.CodingPrinciples, out var principles);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);

        var filesSummary = projectMap is not null
            ? $"Language: {projectMap.PrimaryLanguage}, Frameworks: {string.Join(", ", projectMap.Frameworks)}\n" +
              $"Modules: {projectMap.Modules.Count} total ({projectMap.TestProjects.Count} test project(s))"
            : "Project analysis not available";

        // Code-area signals derived from module paths for skill selection
        var signalAnalysis = "";
        if (projectMap is not null)
        {
            var paths = projectMap.Modules.Select(m => m.Path.ToLowerInvariant()).ToList();
            var hasDirectObjectReferences = paths.Any(p =>
                p.Contains("controller") || p.Contains("handler") || p.Contains("endpoint"));
            var hasInputEntryPoints = paths.Any(p =>
                p.Contains("request") || p.Contains("dto") || p.Contains("form") || p.Contains("input"));
            var hasAuthGuardChanges = paths.Any(p =>
                p.Contains("auth") || p.Contains("guard") || p.Contains("policy") || p.Contains("permission"));
            var hasErrorHandlingChanges = paths.Any(p =>
                p.Contains("exception") || p.Contains("error") || p.Contains("middleware"));

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
