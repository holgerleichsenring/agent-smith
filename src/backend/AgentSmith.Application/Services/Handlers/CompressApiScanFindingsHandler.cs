using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Compresses raw API scan findings (Nuclei, Spectral, ZAP) into compact summaries
/// and skill-specific category slices. Reduces token usage by routing findings
/// to the skills that need them. p0151g: also preserves a deterministic structured
/// top-N (<see cref="ScannerTopFindings"/>) so downstream skills can cite specific
/// template_ids / matched URLs without re-deriving them from prose.
/// </summary>
public sealed class CompressApiScanFindingsHandler(
    NucleiTopSelector nucleiTopSelector,
    ZapTopSelector zapTopSelector,
    SpectralTopSelector spectralTopSelector,
    ILogger<CompressApiScanFindingsHandler> logger)
    : ICommandHandler<CompressApiScanFindingsContext>
{
    public Task<CommandResult> ExecuteAsync(
        CompressApiScanFindingsContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;

        pipeline.TryGet<NucleiResult>(ContextKeys.NucleiResult, out var nuclei);
        pipeline.TryGet<SpectralResult>(ContextKeys.SpectralResult, out var spectral);
        pipeline.TryGet<ZapResult>(ContextKeys.ZapResult, out var zap);

        var summary = ApiScanFindingsCompressor.BuildSummary(nuclei, spectral, zap);
        var slices = ApiScanFindingsCompressor.BuildCategorySlices(nuclei, spectral, zap);
        var topFindings = new ScannerTopFindings(
            Nuclei: nucleiTopSelector.SelectTop(nuclei?.Findings),
            Zap: zapTopSelector.SelectTop(zap?.Findings),
            Spectral: spectralTopSelector.SelectTop(spectral?.Findings));

        pipeline.Set(ContextKeys.ApiScanFindingsSummary, summary);
        pipeline.Set(ContextKeys.ApiScanFindingsByCategory, slices);
        pipeline.Set(ContextKeys.ScannerTopFindings, topFindings);

        var totalFindings = (nuclei?.Findings.Count ?? 0)
                          + (spectral?.Findings.Count ?? 0)
                          + (zap?.Findings.Count ?? 0);

        logger.LogInformation(
            "Compressed {Total} API scan findings into summary ({SummaryLen} chars), {SliceCount} category slices, and top-{TopCount} structured anchors",
            totalFindings, summary.Length, slices.Count, topFindings.TotalCount);

        return Task.FromResult(CommandResult.Ok(
            $"Compressed {totalFindings} findings into {slices.Count} slices + {topFindings.TotalCount} structured anchors"));
    }
}
