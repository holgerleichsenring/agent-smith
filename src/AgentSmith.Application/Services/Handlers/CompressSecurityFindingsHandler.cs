using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Compresses raw security findings from StaticPatternScan, GitHistoryScan,
/// and DependencyAudit into compact summaries and skill-specific slices.
/// Reduces token usage by ~74% on findings context.
/// </summary>
public sealed class CompressSecurityFindingsHandler(
    ILogger<CompressSecurityFindingsHandler> logger)
    : ICommandHandler<CompressSecurityFindingsContext>
{
    public Task<CommandResult> ExecuteAsync(
        CompressSecurityFindingsContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;

        pipeline.TryGet<StaticScanResult>(ContextKeys.StaticScanResult, out var staticResult);
        pipeline.TryGet<GitHistoryScanResult>(ContextKeys.GitHistoryScanResult, out var historyResult);
        pipeline.TryGet<DependencyAuditResult>(ContextKeys.DependencyAuditResult, out var depResult);

        var summary = SecurityFindingsCompressor.BuildSummary(staticResult, historyResult, depResult);
        var slices = SecurityFindingsCompressor.BuildCategorySlices(staticResult, historyResult, depResult);

        pipeline.Set(ContextKeys.SecurityFindingsSummary, summary);
        pipeline.Set(ContextKeys.SecurityFindingsByCategory, slices);

        var totalFindings = (staticResult?.Findings.Count ?? 0)
                          + (historyResult?.Findings.Count ?? 0)
                          + (depResult?.Findings.Count ?? 0);

        logger.LogInformation(
            "Compressed {Total} findings into summary ({SummaryLen} chars) and {SliceCount} category slices",
            totalFindings, summary.Length, slices.Count);

        return Task.FromResult(CommandResult.Ok(
            $"Compressed {totalFindings} findings into {slices.Count} category slices"));
    }
}
