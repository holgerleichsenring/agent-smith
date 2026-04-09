using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
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
        pipeline.TryGet<string>(ContextKeys.SecurityFindingsSummary, out var findingsSummary);
        pipeline.TryGet<Dictionary<string, string>>(ContextKeys.SecurityFindingsByCategory, out var categorySlices);

        var filesSummary = codeAnalysis is not null
            ? $"Language: {codeAnalysis.Language}, Framework: {codeAnalysis.Framework}\n" +
              $"Files: {codeAnalysis.FileStructure.Count} total\n" +
              $"Key files: {string.Join(", ", codeAnalysis.FileStructure.Take(30))}\n" +
              $"Dependencies: {string.Join(", ", codeAnalysis.Dependencies.Take(20))}"
            : "Code analysis not available";

        // Get skill-specific findings slice using ActiveSkill (set by SkillRoundHandlerBase)
        var skillFindings = "";
        if (categorySlices is not null)
        {
            pipeline.TryGet<string>(ContextKeys.ActiveSkill, out var activeSkill);
            if (activeSkill is not null)
            {
                // Prefer orchestration-declared input categories over hardcoded mapping
                var roles = pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                    ContextKeys.AvailableRoles, out var r) ? r : null;
                var inputCategories = roles?.FirstOrDefault(x => x.Name == activeSkill)
                    ?.Orchestration?.InputCategories;
                skillFindings = SecurityFindingsCompressor.GetSliceForSkill(
                    activeSkill, categorySlices, inputCategories);
            }
        }

        return $"""
            ## Security Scan Target
            {filesSummary}

            {findingsSummary ?? ""}

            {(string.IsNullOrEmpty(skillFindings) ? "" : $"## Detailed Findings (your focus area)\n{skillFindings}")}

            Focus your analysis on security vulnerabilities, not functionality.
            Validate the automated findings above. Add context, confirm or dispute
            severity, and identify issues the pattern scanner missed.
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
