using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Compresses raw API scan findings (Nuclei, Spectral, ZAP) into compact summaries
/// and skill-specific category slices. Reduces token usage by routing findings
/// to the skills that need them.
/// </summary>
public sealed class CompressApiScanFindingsHandler(
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

        pipeline.Set(ContextKeys.ApiScanFindingsSummary, summary);
        pipeline.Set(ContextKeys.ApiScanFindingsByCategory, slices);

        var totalFindings = (nuclei?.Findings.Count ?? 0)
                          + (spectral?.Findings.Count ?? 0)
                          + (zap?.Findings.Count ?? 0);

        logger.LogInformation(
            "Compressed {Total} API scan findings into summary ({SummaryLen} chars) and {SliceCount} category slices",
            totalFindings, summary.Length, slices.Count);

        return Task.FromResult(CommandResult.Ok(
            $"Compressed {totalFindings} findings into {slices.Count} category slices"));
    }
}
