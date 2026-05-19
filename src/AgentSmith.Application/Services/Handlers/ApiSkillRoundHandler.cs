using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Application.Services.SkillRounds.Strategies;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// API-security skill round handler. Provides the Swagger specification +
/// per-skill API-scan findings as domain context. Used by the
/// api-security-scan pipeline.
/// </summary>
public sealed class ApiSkillRoundHandler(
    IDiscussionRoundExecutor discussionExecutor,
    IStructuredRoundExecutor structuredExecutor,
    ApiSkillPromptStrategy strategy,
    ILogger<ApiSkillRoundHandler> logger)
    : SkillRoundHandlerBase(discussionExecutor, structuredExecutor),
      ICommandHandler<ApiSecuritySkillRoundContext>
{
    protected override ILogger Logger => logger;
    protected override ISkillPromptStrategy Strategy => strategy;
    private readonly SwaggerSpecTextRenderer _renderer = new();
    private readonly HttpProbeRunner? _probeRunner = httpProbeRunner;

    protected override ILogger Logger => logger;
    protected override string SkillRoundCommandName => "ApiSecuritySkillRoundCommand";

    protected override string BuildDomainSection(PipelineContext pipeline)
    {
        var (stable, perSkill) = BuildDomainSectionParts(pipeline);
        return string.IsNullOrEmpty(perSkill) ? stable : $"{stable}\n\n{perSkill}";
    }

    protected override (string Stable, string PerSkill) BuildDomainSectionParts(PipelineContext pipeline)
    {
        pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);
        pipeline.TryGet<bool>(ContextKeys.ActiveMode, out var activeMode);
        pipeline.TryGet<string>(ContextKeys.ActiveSkill, out var activeSkill);

        var stable = $"""
            {projectBriefBuilder.Build(pipeline)}

            Project language: {conceptsFactory(pipeline).GetEnum("project_language")}.

            ## API Security Scan Target
            Title: {spec?.Title ?? "Unknown"}
            Version: {spec?.Version ?? "Unknown"}

            ## Swagger Specification (compact text view)
            {(spec is not null ? _renderer.Render(spec) : "Not available")}

            {BuildSummarySection(pipeline)}{BuildProbeResultsSection(pipeline)}{BuildHeadersBaselineSection(activeSkill)}
            """.Trim();

        var perSkill = $"""
            ## Mode
            {(activeMode
                ? "Active mode — you may request HTTP probes using {\"probe\": {\"persona\": \"...\", \"method\": \"...\", \"url\": \"...\"}} JSON blocks."
                : "Passive mode — HTTP probing is not available. Analyze schema only.")}

            {BuildPerSkillFindingsSection(pipeline)}
            Analyze the findings relevant to your role.
            """.Trim();

        return (stable, perSkill);
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
            ? ""
            : $"\n## Relevant Findings for This Skill\n{skillFindings}\n\n";
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

    public async Task<CommandResult> ExecuteAsync(
        ApiSecuritySkillRoundContext context, CancellationToken cancellationToken)
    {
        context.Pipeline.Set(ContextKeys.AgentConfig, context.AgentConfig);
        return await ExecuteRoundAsync(
            context.SkillName, context.Round, context.Pipeline, cancellationToken);
    }
}
