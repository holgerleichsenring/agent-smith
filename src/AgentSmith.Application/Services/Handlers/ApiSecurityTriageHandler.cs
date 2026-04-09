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
    ISkillGraphBuilder skillGraphBuilder,
    ILogger<ApiSecurityTriageHandler> logger)
    : TriageHandlerBase, ICommandHandler<ApiSecurityTriageContext>
{
    protected override ILogger Logger => logger;
    protected override string SkillRoundCommandName => "ApiSecuritySkillRoundCommand";
    protected override ISkillGraphBuilder? GraphBuilder => skillGraphBuilder;

    protected override string BuildUserPrompt(PipelineContext pipeline)
    {
        pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);
        pipeline.TryGet<NucleiResult>(ContextKeys.NucleiResult, out var nuclei);

        var specSummary = "Swagger spec not available";
        var endpointDetails = "";
        var authDetails = "";
        var signalAnalysis = "";

        if (spec is not null)
        {
            specSummary = $"API: {spec.Title} v{spec.Version}, {spec.Endpoints.Count} endpoints";

            endpointDetails = string.Join("\n", spec.Endpoints.Select(e =>
                $"  {e.Method} {e.Path} (auth: {e.RequiresAuth}, params: {string.Join(", ", e.Parameters.Select(p => $"{p.Name}[{p.In}]"))})"));

            authDetails = spec.SecuritySchemes.Count > 0
                ? string.Join("\n", spec.SecuritySchemes.Select(s =>
                    $"  {s.Name}: type={s.Type}, in={s.In ?? "n/a"}, scheme={s.Scheme ?? "n/a"}"))
                : "  No security schemes defined";

            var hasIdPaths = spec.Endpoints.Any(e => e.Path.Contains("{id}") || e.Path.Contains("{"));
            var hasAuthScheme = spec.SecuritySchemes.Count > 0;
            var allUnprotected = spec.Endpoints.All(e => !e.RequiresAuth);
            var hasQueryParams = spec.Endpoints.Any(e => e.Parameters.Any(p => p.In == "query"));
            var hasBulkEndpoints = spec.Endpoints.Any(e => e.Path.Contains("bulk") || e.Path.Contains("batch"));

            signalAnalysis = $"""
                Signals detected:
                - ID-based paths (BOLA risk): {hasIdPaths}
                - Auth scheme declared: {hasAuthScheme}
                - All endpoints unprotected: {allUnprotected}
                - Query parameters present: {hasQueryParams}
                - Bulk operation endpoints: {hasBulkEndpoints}
                """;
        }

        var nucleiSummary = "No Nuclei findings";
        var nucleiDetails = "";
        if (nuclei is not null && nuclei.Findings.Count > 0)
        {
            nucleiSummary = $"{nuclei.Findings.Count} findings (critical: {nuclei.Findings.Count(f => f.Severity == "critical")}, " +
                            $"high: {nuclei.Findings.Count(f => f.Severity == "high")}, " +
                            $"medium: {nuclei.Findings.Count(f => f.Severity == "medium")}, " +
                            $"low: {nuclei.Findings.Count(f => f.Severity == "low")})";
            nucleiDetails = string.Join("\n", nuclei.Findings.Select(f =>
                $"  [{f.Severity.ToUpperInvariant()}] {f.Name} — {f.MatchedUrl}"));
        }

        return $"""
            ## API Overview
            {specSummary}

            ## Endpoints
            {endpointDetails}

            ## Authentication Configuration
            {authDetails}

            ## {signalAnalysis}

            ## Nuclei Scan Results
            {nucleiSummary}
            {nucleiDetails}

            Select ALL roles whose triggers match the signals above.
            Every role that has relevant work to do should participate.
            Only exclude a role if there is genuinely nothing for it to review.
            """;
    }

    public async Task<CommandResult> ExecuteAsync(
        ApiSecurityTriageContext context, CancellationToken cancellationToken)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);
        return await TriageAsync(context.Pipeline, llmClient, cancellationToken);
    }
}
