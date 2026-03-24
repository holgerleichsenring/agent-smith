using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Security-scan skill round: provides code analysis as domain context.
/// Used by the security-scan pipeline.
/// </summary>
public sealed class SecuritySkillRoundHandler(
    ILlmClientFactory llmClientFactory,
    ILogger<SecuritySkillRoundHandler> logger)
    : SkillRoundHandlerBase, ICommandHandler<SecuritySkillRoundContext>
{
    protected override ILogger Logger => logger;
    protected override string SkillRoundCommandName => "SecuritySkillRoundCommand";

    protected override string BuildDomainSection(PipelineContext pipeline)
    {
        pipeline.TryGet<CodeAnalysis>(ContextKeys.CodeAnalysis, out var codeAnalysis);

        var filesSummary = codeAnalysis is not null
            ? $"Language: {codeAnalysis.Language}, Framework: {codeAnalysis.Framework}\n" +
              $"Files: {codeAnalysis.FileStructure.Count} total\n" +
              $"Key files: {string.Join(", ", codeAnalysis.FileStructure.Take(30))}\n" +
              $"Dependencies: {string.Join(", ", codeAnalysis.Dependencies.Take(20))}"
            : "Code analysis not available";

        return $"""
            ## Security Scan Target
            {filesSummary}

            Focus your analysis on security vulnerabilities, not functionality.
            Look for: injection risks, auth flaws, secrets exposure, unsafe deserialization,
            missing input validation, and insecure configurations.
            """;
    }

    public async Task<CommandResult> ExecuteAsync(
        SecuritySkillRoundContext context, CancellationToken cancellationToken)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);
        return await ExecuteRoundAsync(
            context.SkillName, context.Round, context.Pipeline, llmClient, cancellationToken);
    }
}
