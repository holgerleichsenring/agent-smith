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
    ILogger<SecurityTriageHandler> logger)
    : TriageHandlerBase, ICommandHandler<SecurityTriageContext>
{
    protected override ILogger Logger => logger;

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

        return $"""
            ## Security Scan Target
            {filesSummary}

            ## Project Context
            {projectContext ?? "Not available"}

            ## Coding Principles
            {principles ?? "Not available"}

            Determine which security specialist roles should review this codebase.
            Consider the language, framework, dependencies, and file structure
            to select the most relevant security reviewers.
            """;
    }

    public async Task<CommandResult> ExecuteAsync(
        SecurityTriageContext context, CancellationToken cancellationToken)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);
        return await TriageAsync(context.Pipeline, llmClient, cancellationToken);
    }
}
