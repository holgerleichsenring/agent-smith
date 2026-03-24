using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Triages based on swagger spec and Nuclei findings to determine which
/// API security specialist roles should participate.
/// </summary>
public sealed class ApiSecurityTriageHandler(
    ILlmClientFactory llmClientFactory,
    ILogger<ApiSecurityTriageHandler> logger)
    : TriageHandlerBase, ICommandHandler<ApiSecurityTriageContext>
{
    protected override ILogger Logger => logger;
    protected override string SkillRoundCommandName => "ApiSecuritySkillRoundCommand";

    protected override string BuildUserPrompt(PipelineContext pipeline)
    {
        pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);
        pipeline.TryGet<NucleiResult>(ContextKeys.NucleiResult, out var nuclei);

        var specSummary = spec is not null
            ? $"API: {spec.Title} v{spec.Version}\n" +
              $"Endpoints: {spec.Endpoints.Count}\n" +
              $"Auth schemes: {string.Join(", ", spec.SecuritySchemes.Select(s => $"{s.Name} ({s.Type})"))}\n" +
              $"Methods: {string.Join(", ", spec.Endpoints.GroupBy(e => e.Method).Select(g => $"{g.Key}:{g.Count()}"))}"
            : "Swagger spec not available";

        var nucleiSummary = nuclei is not null && nuclei.Findings.Count > 0
            ? $"Nuclei findings: {nuclei.Findings.Count} total\n" +
              $"Critical: {nuclei.Findings.Count(f => f.Severity == "critical")}\n" +
              $"High: {nuclei.Findings.Count(f => f.Severity == "high")}\n" +
              $"Medium: {nuclei.Findings.Count(f => f.Severity == "medium")}"
            : "No Nuclei findings (clean scan or scan skipped)";

        return $"""
            ## API Security Scan Target
            {specSummary}

            ## Nuclei Scan Results
            {nucleiSummary}

            Determine which API security specialist roles should review this API.
            Consider the endpoint patterns, auth configuration, Nuclei findings,
            and parameter types to select the most relevant reviewers.
            """;
    }

    public async Task<CommandResult> ExecuteAsync(
        ApiSecurityTriageContext context, CancellationToken cancellationToken)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);
        return await TriageAsync(context.Pipeline, llmClient, cancellationToken);
    }
}
