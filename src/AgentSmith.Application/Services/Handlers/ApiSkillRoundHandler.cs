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
/// API security skill round: provides swagger spec + Nuclei findings as domain context.
/// Used by the api-security-scan pipeline.
/// </summary>
public sealed class ApiSkillRoundHandler(
    ILlmClientFactory llmClientFactory,
    ISkillPromptBuilder promptBuilder,
    IGateOutputHandler gateOutputHandler,
    IUpstreamContextBuilder upstreamContextBuilder,
    ILogger<ApiSkillRoundHandler> logger)
    : SkillRoundHandlerBase(promptBuilder, gateOutputHandler, upstreamContextBuilder),
      ICommandHandler<ApiSecuritySkillRoundContext>
{
    private readonly SwaggerSpecCompressor _compressor = new();

    protected override ILogger Logger => logger;
    protected override string SkillRoundCommandName => "ApiSecuritySkillRoundCommand";

    protected override string BuildDomainSection(PipelineContext pipeline)
    {
        pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);

        var compressedSpec = spec is not null
            ? _compressor.Compress(spec)
            : "Not available";

        var findingsSection = BuildFindingsFromSlices(pipeline);
        if (string.IsNullOrWhiteSpace(findingsSection))
            findingsSection = BuildFindingsRaw(pipeline);

        return $"""
            ## API Security Scan Target
            Title: {spec?.Title ?? "Unknown"}
            Version: {spec?.Version ?? "Unknown"}

            ## Swagger Specification (compressed)
            {compressedSpec}

            {findingsSection}

            Analyze the findings relevant to your role.
            Focus on response schema field combinations, enum definitions, REST semantics,
            route consistency, missing constraints, and contextualize the scanner findings.
            """;
    }

    private static string BuildFindingsFromSlices(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.ApiScanFindingsSummary, out var summary)
            || summary is null)
            return string.Empty;

        pipeline.TryGet<Dictionary<string, string>>(ContextKeys.ApiScanFindingsByCategory, out var slices);
        if (slices is null || slices.Count == 0)
            return summary;

        pipeline.TryGet<string>(ContextKeys.ActiveSkill, out var activeSkill);
        pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, out var roles);

        var inputCategories = roles?.FirstOrDefault(r =>
            r.Name.Equals(activeSkill, StringComparison.OrdinalIgnoreCase))
            ?.Orchestration?.InputCategories;

        var skillFindings = ApiScanFindingsCompressor.GetSliceForSkill(
            activeSkill ?? "", slices, inputCategories);

        if (string.IsNullOrWhiteSpace(skillFindings))
            return summary;

        return $"""
            {summary}

            ## Relevant Findings for This Skill
            {skillFindings}
            """;
    }

    private static string BuildFindingsRaw(PipelineContext pipeline)
    {
        pipeline.TryGet<NucleiResult>(ContextKeys.NucleiResult, out var nuclei);
        pipeline.TryGet<SpectralResult>(ContextKeys.SpectralResult, out var spectral);

        var nucleiFindings = nuclei is not null && nuclei.Findings.Count > 0
            ? string.Join("\n", nuclei.Findings.Select(f =>
                $"  [{f.Severity.ToUpperInvariant()}] {f.TemplateId}: {f.Name} — {f.MatchedUrl}"
                + (f.Description is not null ? $"\n    {f.Description}" : "")))
            : "No findings from Nuclei scan";

        var spectralFindings = spectral is not null && spectral.Findings.Count > 0
            ? string.Join("\n", spectral.Findings.Select(f =>
                $"  [{f.Severity.ToUpperInvariant()}] {f.Code}: {f.Message} — {f.Path} (line {f.Line})"))
            : "No findings from Spectral lint";

        return $"""
            ## Nuclei Scan Findings ({nuclei?.Findings.Count ?? 0} total)
            {nucleiFindings}

            ## Spectral Lint Findings ({spectral?.Findings.Count ?? 0} total, {spectral?.ErrorCount ?? 0} errors, {spectral?.WarnCount ?? 0} warnings)
            {spectralFindings}
            """;
    }

    public async Task<CommandResult> ExecuteAsync(
        ApiSecuritySkillRoundContext context, CancellationToken cancellationToken)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);
        return await ExecuteRoundAsync(
            context.SkillName, context.Round, context.Pipeline, llmClient, cancellationToken);
    }
}
