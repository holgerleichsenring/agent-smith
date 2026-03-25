using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
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
    ILogger<ApiSkillRoundHandler> logger)
    : SkillRoundHandlerBase, ICommandHandler<ApiSecuritySkillRoundContext>
{
    protected override ILogger Logger => logger;
    protected override string SkillRoundCommandName => "ApiSecuritySkillRoundCommand";

    protected override string BuildDomainSection(PipelineContext pipeline)
    {
        pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);
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

        var swaggerJson = spec?.RawJson ?? "Not available";

        return $"""
            ## API Security Scan Target
            Title: {spec?.Title ?? "Unknown"}
            Version: {spec?.Version ?? "Unknown"}

            ## Full Swagger Specification (swagger.json)
            ```json
            {swaggerJson}
            ```

            ## Nuclei Scan Findings ({nuclei?.Findings.Count ?? 0} total)
            {nucleiFindings}

            ## Spectral Lint Findings ({spectral?.Findings.Count ?? 0} total, {spectral?.ErrorCount ?? 0} errors, {spectral?.WarnCount ?? 0} warnings)
            {spectralFindings}

            Analyze the full swagger.json schema for structural security issues.
            Focus on response schema field combinations, enum definitions, REST semantics,
            route consistency, missing constraints, and contextualize the Spectral findings.
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
