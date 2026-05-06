using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// API security skill round: provides swagger spec + Nuclei findings as domain context,
/// plus code-aware excerpts when ApiSourceAvailable. Splits the domain section into
/// a stable prefix (cached across same-round calls) and a per-skill suffix.
/// </summary>
public sealed class ApiSkillRoundHandler(
    IChatClientFactory chatClientFactory,
    ISkillPromptBuilder promptBuilder,
    IGateRetryCoordinator gateRetryCoordinator,
    IUpstreamContextBuilder upstreamContextBuilder,
    StructuredOutputInstructionBuilder instructionBuilder,
    IProjectBriefBuilder projectBriefBuilder,
    IBaselineLoader baselineLoader,
    HttpProbeRunner? httpProbeRunner,
    ILogger<ApiSkillRoundHandler> logger)
    : SkillRoundHandlerBase(promptBuilder, gateRetryCoordinator, upstreamContextBuilder, instructionBuilder, chatClientFactory),
      ICommandHandler<ApiSecuritySkillRoundContext>
{
    private readonly SwaggerSpecCompressor _compressor = new();
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

            ## API Security Scan Target
            Title: {spec?.Title ?? "Unknown"}
            Version: {spec?.Version ?? "Unknown"}

            ## Swagger Specification (compressed)
            {(spec is not null ? _compressor.Compress(spec) : "Not available")}

            {BuildSummarySection(pipeline)}{BuildCodeContextSection(pipeline)}{BuildProbeResultsSection(pipeline)}{BuildHeadersBaselineSection(activeSkill)}
            """.Trim();

        var perSkill = $"""
            ## Mode
            {(activeMode
                ? "Active mode — you may request HTTP probes using {\"probe\": {\"persona\": \"...\", \"method\": \"...\", \"url\": \"...\"}} JSON blocks."
                : "Passive mode — HTTP probing is not available. Analyze schema only.")}

            {BuildPerSkillFindingsSection(pipeline)}{BuildPerSkillCodeSection(pipeline)}
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

    private string BuildCodeContextSection(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<bool>(ContextKeys.ApiSourceAvailable, out var avail) || !avail) return "";
        if (!pipeline.TryGet<ApiCodeContext>(ContextKeys.ApiCodeContext, out var code) || code is null) return "";

        var routeLines = code.RoutesToHandlers
            .Where(r => r.Confidence >= 0.5)
            .Take(40)
            .Select(r => $"  {r.Method} {r.Path} → {r.File}:{r.StartLine} ({r.Framework}, conf {r.Confidence:F1})");
        return $"\n## Code Context (mapped routes, conf {code.MappingConfidence:P0})\n{string.Join("\n", routeLines)}\n";
    }

    private static string BuildPerSkillCodeSection(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<bool>(ContextKeys.ApiSourceAvailable, out var avail) || !avail) return "";
        if (!pipeline.TryGet<ApiCodeContext>(ContextKeys.ApiCodeContext, out var code) || code is null) return "";
        pipeline.TryGet<string>(ContextKeys.ActiveSkill, out var active);

        var excerpts = active?.ToLowerInvariant() switch
        {
            "auth-config-reviewer" => code.AuthBootstrapFiles.Concat(code.SecurityMiddlewareRegistrations),
            "upload-validator-reviewer" => code.UploadHandlers,
            "ownership-checker" => code.RoutesToHandlers
                .Where(r => r.Confidence >= 0.5 && r.Method is "POST" or "PUT" or "DELETE" or "PATCH" or "GET")
                .Select(r => new SourceFileExcerpt(r.File, r.StartLine, r.EndLine, r.HandlerSnippet, $"handler for {r.Method} {r.Path}")),
            "controller-implementation-reviewer" => code.RoutesToHandlers
                .Where(r => r.Confidence >= 0.5 && r.Method is "POST" or "PUT" or "DELETE" or "PATCH")
                .Select(r => new SourceFileExcerpt(r.File, r.StartLine, r.EndLine, r.HandlerSnippet, $"handler for {r.Method} {r.Path}")),
            "security-headers-auditor" => code.AuthBootstrapFiles.Concat(code.SecurityMiddlewareRegistrations),
            _ => null
        };
        if (excerpts is null) return "";

        var rendered = string.Join("\n\n", excerpts.Take(20).Select(e =>
            $"### {e.File}:{e.StartLine} — {e.Reason}\n```\n{e.Content}\n```"));
        var correlationsSection = BuildCorrelationsForSkill(pipeline, active);
        var combined = $"{(string.IsNullOrEmpty(rendered) ? "" : $"\n## Source Excerpts\n{rendered}\n")}{correlationsSection}";
        return string.IsNullOrEmpty(combined) ? "" : $"{combined}\n";
    }

    private static string BuildCorrelationsForSkill(PipelineContext pipeline, string? active)
    {
        if (active is null) return "";
        if (!active.Equals("controller-implementation-reviewer", StringComparison.OrdinalIgnoreCase)) return "";
        if (!pipeline.TryGet<IReadOnlyList<FindingHandlerCorrelation>>(
                ContextKeys.FindingHandlerCorrelations, out var correlations)
            || correlations is null) return "";

        var withHandler = correlations.Where(c => c.Handler is not null).Take(20).ToList();
        if (withHandler.Count == 0) return "";

        var lines = withHandler.Select(c =>
            $"- [{c.Severity.ToUpperInvariant()}] {c.FindingSource}/{c.FindingId} — {c.Method} {c.Url} → {c.Handler!.File}:{c.Handler.StartLine}");
        return $"\n## Correlated Findings (for handlers above)\n{string.Join("\n", lines)}\n";
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
