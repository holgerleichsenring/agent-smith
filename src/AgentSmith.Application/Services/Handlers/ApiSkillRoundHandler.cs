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

        var endpointList = spec is not null
            ? string.Join("\n", spec.Endpoints.Take(50).Select(e =>
                $"  {e.Method} {e.Path} (auth: {e.RequiresAuth}, params: {e.Parameters.Count})"))
            : "Not available";

        var authSchemes = spec is not null
            ? string.Join("\n", spec.SecuritySchemes.Select(s =>
                $"  {s.Name}: {s.Type} (in: {s.In ?? "n/a"}, scheme: {s.Scheme ?? "n/a"})"))
            : "Not available";

        var findings = nuclei is not null && nuclei.Findings.Count > 0
            ? string.Join("\n", nuclei.Findings.Select(f =>
                $"  [{f.Severity.ToUpperInvariant()}] {f.Name} — {f.MatchedUrl}"))
            : "No findings from automated scan";

        return $"""
            ## API Security Scan Target
            Title: {spec?.Title ?? "Unknown"}
            Version: {spec?.Version ?? "Unknown"}

            ## Endpoints
            {endpointList}

            ## Authentication Schemes
            {authSchemes}

            ## Nuclei Scan Findings
            {findings}

            Focus on API-specific vulnerabilities (OWASP API Security Top 10 2023).
            Evaluate both the automated scan results and the API design itself.
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
