using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.SkillRounds.Strategies;

/// <summary>
/// p0147d: API-security domain section. Stable part: project brief + Swagger
/// summary + probe results + headers baseline. Per-skill part: active/passive
/// mode marker + the skill's relevant API-scan finding slice + (p0151c) the
/// running observation bus.
/// </summary>
public sealed class ApiSkillPromptStrategy(
    IProjectBriefBuilder projectBriefBuilder,
    IBaselineLoader baselineLoader,
    ObservationBusProjector busProjector,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory) : ISkillPromptStrategy
{
    public string SkillRoundCommandName => "ApiSecuritySkillRoundCommand";

    public (string Stable, string PerSkill) BuildDomainSectionParts(PipelineContext pipeline)
    {
        pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);
        pipeline.TryGet<bool>(ContextKeys.ActiveMode, out var activeMode);
        pipeline.TryGet<string>(ContextKeys.ActiveSkill, out var activeSkill);

        var renderer = new SwaggerSpecTextRenderer();
        var stable = $"""
            {projectBriefBuilder.Build(pipeline)}

            Project language: {conceptsFactory(pipeline).GetString("project_language")}.

            ## API Security Scan Target
            Title: {spec?.Title ?? "Unknown"}
            Version: {spec?.Version ?? "Unknown"}

            ## Swagger Specification (compressed)
            {(spec is not null ? renderer.Render(spec) : "Not available")}

            {BuildSummarySection(pipeline)}{BuildProbeResultsSection(pipeline)}{BuildHeadersBaselineSection(activeSkill)}
            """.Trim();

        var perSkill = $"""
            ## Mode
            {(activeMode
                ? "Active mode — you may request HTTP probes using {\"probe\": {\"persona\": \"...\", \"method\": \"...\", \"url\": \"...\"}} JSON blocks."
                : "Passive mode — HTTP probing is not available. Analyze schema only.")}

            {BuildPerSkillFindingsSection(pipeline)}{BuildObservationsSoFarSection(pipeline)}
            Analyze the findings relevant to your role.
            """.Trim();
        return (stable, perSkill);
    }

    private string BuildObservationsSoFarSection(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var observations)
            || observations is null || observations.Count == 0)
            return "";
        return $"\n## Observations So Far\n```json\n{busProjector.Project(observations)}\n```\nYou may treat these as hints from prior rounds. Verify with your own tools before extending or contradicting them.\n";
    }

    private static string BuildSummarySection(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.ApiScanFindingsSummary, out var summary)
            || string.IsNullOrWhiteSpace(summary))
            return "";
        return $"\n## Findings Summary\n{summary}\n";
    }

    private string BuildHeadersBaselineSection(string? activeSkill)
    {
        if (activeSkill is null) return "";
        if (!activeSkill.Equals("security-headers-auditor", StringComparison.OrdinalIgnoreCase)) return "";
        var baseline = baselineLoader.Load("api-headers");
        return baseline is null ? "" : $"\n## Headers Baseline\n```yaml\n{baseline.TrimEnd()}\n```\n";
    }

    private static string BuildPerSkillFindingsSection(PipelineContext pipeline)
    {
        pipeline.TryGet<Dictionary<string, string>>(ContextKeys.ApiScanFindingsByCategory, out var slices);
        if (slices is null || slices.Count == 0) return "";
        pipeline.TryGet<string>(ContextKeys.ActiveSkill, out var activeSkill);
        pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, out var roles);
        var inputCategories = roles?.FirstOrDefault(r =>
            r.Name.Equals(activeSkill, StringComparison.OrdinalIgnoreCase))
            ?.Orchestration?.InputCategories;
        var skillFindings = ApiScanFindingsCompressor.GetSliceForSkill(
            activeSkill ?? "", slices, inputCategories);
        return string.IsNullOrWhiteSpace(skillFindings)
            ? "" : $"\n## Relevant Findings for This Skill\n{skillFindings}\n\n";
    }

    private static string BuildProbeResultsSection(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<List<HttpProbeResult>>(ContextKeys.HttpProbeResults, out var results)
            || results is null || results.Count == 0)
            return "";
        var lines = results.Select(r =>
            $"  [{r.Persona}] {r.Method} {r.Url} → {r.StatusCode} ({r.DurationMs}ms)");
        return $"\n## HTTP Probe Results (from previous rounds)\n{string.Join("\n", lines)}\n";
    }
}
