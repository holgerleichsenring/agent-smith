using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds the triage user prompt for the API security scan. Surfaces both
/// schema-level signals (BOLA, IDOR, anonymous access, …) and source-derived
/// signals (auth bootstrap present, upload handlers, mapped state-changing routes).
/// </summary>
public sealed class ApiSecurityTriagePromptBuilder
{
    public string Build(PipelineContext pipeline)
    {
        pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);
        pipeline.TryGet<NucleiResult>(ContextKeys.NucleiResult, out var nuclei);
        pipeline.TryGet<bool>(ContextKeys.ActiveMode, out var activeMode);
        pipeline.TryGet<bool>(ContextKeys.ApiSourceAvailable, out var sourceAvailable);
        pipeline.TryGet<ApiCodeContext>(ContextKeys.ApiCodeContext, out var code);

        return $"""
            ## API Overview
            {SpecSummary(spec)}

            ## Endpoints
            {EndpointDetails(spec)}

            ## Authentication Configuration
            {AuthDetails(spec)}

            ## Signals
            {SignalAnalysis(spec, activeMode, sourceAvailable, code)}

            ## Nuclei Scan Results
            {NucleiSummary(nuclei)}

            Select ALL roles whose triggers match the signals above.
            Every role that has relevant work to do should participate.
            Only exclude a role if there is genuinely nothing for it to review.
            """;
    }

    private static string SpecSummary(SwaggerSpec? spec) => spec is null
        ? "Swagger spec not available"
        : $"API: {spec.Title} v{spec.Version}, {spec.Endpoints.Count} endpoints";

    private static string EndpointDetails(SwaggerSpec? spec) => spec is null ? "" :
        string.Join("\n", spec.Endpoints.Select(e =>
            $"  {e.Method} {e.Path} (auth: {e.RequiresAuth}, params: {string.Join(", ", e.Parameters.Select(p => $"{p.Name}[{p.In}]"))})"));

    private static string AuthDetails(SwaggerSpec? spec) => spec is null ? "  Spec missing" :
        spec.SecuritySchemes.Count > 0
            ? string.Join("\n", spec.SecuritySchemes.Select(s =>
                $"  {s.Name}: type={s.Type}, in={s.In ?? "n/a"}, scheme={s.Scheme ?? "n/a"}"))
            : "  No security schemes defined";

    private static string SignalAnalysis(
        SwaggerSpec? spec, bool activeMode, bool sourceAvailable, ApiCodeContext? code)
    {
        if (spec is null) return $"- Active mode: {activeMode}\n- Source available: {sourceAvailable}";

        var hasIdPaths = spec.Endpoints.Any(e => e.Path.Contains('{'));
        var allUnprotected = spec.Endpoints.All(e => !e.RequiresAuth);
        var hasQueryParams = spec.Endpoints.Any(e => e.Parameters.Any(p => p.In == "query"));
        var hasBulk = spec.Endpoints.Any(e => e.Path.Contains("bulk") || e.Path.Contains("batch"));
        var hasAnonymous = spec.Endpoints.Any(e => !e.RequiresAuth && e.Method != "OPTIONS");
        var hasUpload = spec.Endpoints.Any(e =>
            e.Parameters.Any(p => p.In == "formData" || p.Name.Contains("file", StringComparison.OrdinalIgnoreCase)));
        var hasDeleteHier = spec.Endpoints.Any(e => e.Method.Equals("DELETE", StringComparison.OrdinalIgnoreCase) && e.Path.Contains('{'));
        var hasAuthBootstrap = code?.AuthBootstrapFiles.Count > 0;
        var hasUploadHandlers = code?.UploadHandlers.Count > 0;
        var hasStateChangingMapped = code?.RoutesToHandlers.Any(r =>
            r.Confidence >= 0.5 && r.Method is "POST" or "PUT" or "DELETE" or "PATCH") ?? false;

        return $"""
            - ID-based paths (BOLA risk): {hasIdPaths}
            - All endpoints unprotected: {allUnprotected}
            - Anonymous endpoints (non-OPTIONS): {hasAnonymous}
            - Query parameters present: {hasQueryParams}
            - Bulk operation endpoints: {hasBulk}
            - File upload endpoints (schema): {hasUpload}
            - DELETE on hierarchical resources: {hasDeleteHier}
            - Active mode (personas authenticated): {activeMode}
            - Source available: {sourceAvailable}
            - Auth bootstrap blocks found in source: {hasAuthBootstrap}
            - Upload handlers found in source: {hasUploadHandlers}
            - State-changing routes mapped to handlers: {hasStateChangingMapped}
            """;
    }

    private static string NucleiSummary(NucleiResult? nuclei)
    {
        if (nuclei is null || nuclei.Findings.Count == 0) return "No Nuclei findings";

        var summary = $"{nuclei.Findings.Count} findings (critical: {nuclei.Findings.Count(f => f.Severity == "critical")}, " +
                      $"high: {nuclei.Findings.Count(f => f.Severity == "high")}, " +
                      $"medium: {nuclei.Findings.Count(f => f.Severity == "medium")}, " +
                      $"low: {nuclei.Findings.Count(f => f.Severity == "low")})";
        var details = string.Join("\n", nuclei.Findings.Select(f =>
            $"  [{f.Severity.ToUpperInvariant()}] {f.Name} — {f.MatchedUrl}"));
        return $"{summary}\n{details}";
    }
}
