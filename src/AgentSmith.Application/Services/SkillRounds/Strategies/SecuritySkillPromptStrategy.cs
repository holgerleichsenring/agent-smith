using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.SkillRounds.Strategies;

/// <summary>
/// p0147d: Security-scan domain section. Stable part: project brief + scan target
/// + findings summary. Per-skill part: the skill's category slice + chain-analyst
/// commodity findings when applicable.
/// </summary>
public sealed class SecuritySkillPromptStrategy(IProjectBriefBuilder projectBriefBuilder) : ISkillPromptStrategy
{
    public string SkillRoundCommandName => "SecuritySkillRoundCommand";

    public (string Stable, string PerSkill) BuildDomainSectionParts(PipelineContext pipeline)
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
        return (stable, BuildPerSkillSection(pipeline));
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
        AppendStaticSection(pipeline, sections);
        AppendHistorySection(pipeline, sections);
        AppendDependencySection(pipeline, sections);
        if (sections.Count == 0) return "";
        return $"""
            ## Commodity Tool Findings (for chain analysis)
            These findings come from automated tools. Use them together with skill findings
            to identify multi-step attack chains and adjust severity accordingly.

            {string.Join("\n\n", sections)}
            """;
    }

    private static void AppendStaticSection(PipelineContext pipeline, List<string> sections)
    {
        if (!pipeline.TryGet<StaticScanResult>(ContextKeys.StaticScanResult, out var s)
            || s is null || s.Findings.Count == 0) return;
        var lines = s.Findings.Take(50).Select(f =>
            $"  [{f.Severity}] {f.Title} — {f.File}:{f.Line} (confidence: {f.Confidence})");
        sections.Add($"### StaticPatternScan ({s.Findings.Count} findings)\n{string.Join("\n", lines)}");
    }

    private static void AppendHistorySection(PipelineContext pipeline, List<string> sections)
    {
        if (!pipeline.TryGet<GitHistoryScanResult>(ContextKeys.GitHistoryScanResult, out var h)
            || h is null || h.Findings.Count == 0) return;
        var lines = h.Findings.Take(30).Select(f =>
            $"  [{f.Severity}] {f.Title} — {f.File}:{f.Line} (commit: {f.CommitHash[..7]}, still in tree: {f.StillInWorkingTree})");
        sections.Add($"### GitHistoryScan ({h.Findings.Count} findings)\n{string.Join("\n", lines)}");
    }

    private static void AppendDependencySection(PipelineContext pipeline, List<string> sections)
    {
        if (!pipeline.TryGet<DependencyAuditResult>(ContextKeys.DependencyAuditResult, out var d)
            || d is null || d.Findings.Count == 0) return;
        var lines = d.Findings.Take(30).Select(f =>
            $"  [{f.Severity}] {f.Package}@{f.Version} — {f.Title} (CVE: {f.Cve ?? "n/a"}, fix: {f.FixVersion ?? "n/a"})");
        sections.Add($"### DependencyAudit ({d.Findings.Count} findings)\n{string.Join("\n", lines)}");
    }
}
