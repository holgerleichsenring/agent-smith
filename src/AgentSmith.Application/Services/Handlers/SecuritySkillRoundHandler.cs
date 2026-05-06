using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Security-scan skill round: provides code analysis as domain context.
/// For chain-analyst (executor), also provides all commodity tool findings.
/// Used by the security-scan pipeline.
/// </summary>
public sealed class SecuritySkillRoundHandler(
    IChatClientFactory chatClientFactory,
    ISkillPromptBuilder promptBuilder,
    IGateRetryCoordinator gateRetryCoordinator,
    IUpstreamContextBuilder upstreamContextBuilder,
    StructuredOutputInstructionBuilder instructionBuilder,
    IProjectBriefBuilder projectBriefBuilder,
    ILogger<SecuritySkillRoundHandler> logger)
    : SkillRoundHandlerBase(promptBuilder, gateRetryCoordinator, upstreamContextBuilder, instructionBuilder, chatClientFactory),
      ICommandHandler<SecuritySkillRoundContext>
{
    protected override ILogger Logger => logger;
    protected override string SkillRoundCommandName => "SecuritySkillRoundCommand";

    protected override string BuildDomainSection(PipelineContext pipeline)
    {
        var (stable, perSkill) = BuildDomainSectionParts(pipeline);
        return string.IsNullOrEmpty(perSkill) ? stable : $"{stable}\n\n{perSkill}";
    }

    protected override (string Stable, string PerSkill) BuildDomainSectionParts(PipelineContext pipeline)
    {
        pipeline.TryGet<string>(ContextKeys.SecurityFindingsSummary, out var findingsSummary);

        var stable = $"""
            {projectBriefBuilder.Build(pipeline)}

            ## Security Scan Target
            {findingsSummary ?? ""}

            Focus your analysis on security vulnerabilities, not functionality.
            Validate the automated findings above. Add context, confirm or dispute
            severity, and identify issues the pattern scanner missed.
            """.Trim();

        var perSkill = BuildPerSkillSection(pipeline);
        return (stable, perSkill);
    }

    private static string BuildPerSkillSection(PipelineContext pipeline)
    {
        pipeline.TryGet<Dictionary<string, string>>(ContextKeys.SecurityFindingsByCategory, out var categorySlices);
        pipeline.TryGet<string>(ContextKeys.ActiveSkill, out var activeSkill);

        var skillFindings = "";
        if (categorySlices is not null && activeSkill is not null)
        {
            var roles = pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var r) ? r : null;
            var inputCategories = roles?.FirstOrDefault(x => x.Name == activeSkill)
                ?.Orchestration?.InputCategories;
            skillFindings = SecurityFindingsCompressor.GetSliceForSkill(
                activeSkill, categorySlices, inputCategories);
        }

        var commoditySection = IsChainAnalyst(activeSkill, pipeline)
            ? BuildCommodityFindingsSection(pipeline) : "";

        var detailedFindings = string.IsNullOrEmpty(skillFindings)
            ? "" : $"## Detailed Findings (your focus area)\n{skillFindings}";
        return $"{detailedFindings}\n\n{commoditySection}".Trim();
    }

    private static bool IsChainAnalyst(string? activeSkill, PipelineContext pipeline)
    {
        if (activeSkill is null) return false;

        if (pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) && roles is not null)
        {
            var role = roles.FirstOrDefault(r =>
                r.Name.Equals(activeSkill, StringComparison.OrdinalIgnoreCase));
            return role?.Orchestration?.Role == OrchestrationRole.Executor;
        }

        return "chain-analyst".Equals(activeSkill, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCommodityFindingsSection(PipelineContext pipeline)
    {
        var sections = new List<string>();

        if (pipeline.TryGet<StaticScanResult>(ContextKeys.StaticScanResult, out var staticResult)
            && staticResult is not null && staticResult.Findings.Count > 0)
        {
            var staticLines = staticResult.Findings.Take(50).Select(f =>
                $"  [{f.Severity}] {f.Title} — {f.File}:{f.Line} (confidence: {f.Confidence})");
            sections.Add($"### StaticPatternScan ({staticResult.Findings.Count} findings)\n{string.Join("\n", staticLines)}");
        }

        if (pipeline.TryGet<GitHistoryScanResult>(ContextKeys.GitHistoryScanResult, out var historyResult)
            && historyResult is not null && historyResult.Findings.Count > 0)
        {
            var historyLines = historyResult.Findings.Take(30).Select(f =>
                $"  [{f.Severity}] {f.Title} — {f.File}:{f.Line} (commit: {f.CommitHash[..7]}, still in tree: {f.StillInWorkingTree})");
            sections.Add($"### GitHistoryScan ({historyResult.Findings.Count} findings)\n{string.Join("\n", historyLines)}");
        }

        if (pipeline.TryGet<DependencyAuditResult>(ContextKeys.DependencyAuditResult, out var depResult)
            && depResult is not null && depResult.Findings.Count > 0)
        {
            var depLines = depResult.Findings.Take(30).Select(f =>
                $"  [{f.Severity}] {f.Package}@{f.Version} — {f.Title} (CVE: {f.Cve ?? "n/a"}, fix: {f.FixVersion ?? "n/a"})");
            sections.Add($"### DependencyAudit ({depResult.Findings.Count} findings)\n{string.Join("\n", depLines)}");
        }

        if (sections.Count == 0)
            return "";

        return $"""
            ## Commodity Tool Findings (for chain analysis)
            These findings come from automated tools. Use them together with skill findings
            to identify multi-step attack chains and adjust severity accordingly.

            {string.Join("\n\n", sections)}
            """;
    }

    public async Task<CommandResult> ExecuteAsync(
        SecuritySkillRoundContext context, CancellationToken cancellationToken)
    {
        context.Pipeline.Set(ContextKeys.AgentConfig, context.AgentConfig);
        return await ExecuteRoundAsync(
            context.SkillName, context.Round, context.Pipeline, cancellationToken);
    }
}
