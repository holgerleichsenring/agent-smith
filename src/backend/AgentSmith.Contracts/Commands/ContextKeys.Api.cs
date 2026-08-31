namespace AgentSmith.Contracts.Commands;

/// <summary>
/// API-security PipelineContext keys: Swagger spec + scanner outputs (Nuclei,
/// Spectral, ZAP), the API-scan target descriptor, aggregated finding summaries,
/// HTTP probe results, and output-format/dir selectors used by IOutputStrategy.
/// </summary>
public static partial class ContextKeys
{
    public const string OutputFormat = "OutputFormat";
    public const string OutputDir = "OutputDir";

    public const string SwaggerSpec = "SwaggerSpec";
    public const string SwaggerPath = "SwaggerPath";

    public const string NucleiResult = "NucleiResult";
    public const string ZapResult = "ZapResult";
    public const string SpectralResult = "SpectralResult";
    public const string ZapFailed = "ZapFailed";

    public const string ApiTarget = "ApiTarget";
    public const string ApiScanFindingsSummary = "ApiScanFindingsSummary";
    public const string ApiScanFindingsByCategory = "ApiScanFindingsByCategory";
    public const string ScannerTopFindings = "ScannerTopFindings";

    public const string HttpProbeResults = "HttpProbeResults";

    /// <summary>2026-08-30-c6ec: the SurfaceDifferenceReport — what the served interface
    /// offers that no declared first-party client was found to exercise, bounded by the
    /// account of what the reading of the client sources covered. Always set by the step:
    /// a run missing an input records WHY it was not computed, because an empty difference
    /// and an uncomputed one read the same and only one of them is a clean bill.</summary>
    public const string SurfaceDifference = "SurfaceDifference";
}
