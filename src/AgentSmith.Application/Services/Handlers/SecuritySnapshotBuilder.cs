using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds SecurityRunSnapshot from current pipeline state.
/// </summary>
internal static class SecuritySnapshotBuilder
{
    internal static SecurityRunSnapshot BuildCurrentSnapshot(
        PipelineContext pipeline, Repository repo)
    {
        pipeline.TryGet<IReadOnlyList<Finding>>(ContextKeys.ExtractedFindings, out var findings);
        findings ??= [];

        pipeline.TryGet<RunCostSummary>(ContextKeys.RunCostSummary, out var costSummary);

        var critical = findings.Count(f => f.Severity.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase));
        var high = findings.Count(f => f.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase));
        var medium = findings.Count(f => f.Severity.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase));

        var scanTypes = DetermineScanTypes(pipeline);

        var topCategories = findings
            .GroupBy(f => f.Title.Split(' ')[0], StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        return new SecurityRunSnapshot(
            Date: DateTimeOffset.UtcNow,
            Branch: repo.CurrentBranch.Value,
            FindingsCritical: critical,
            FindingsHigh: high,
            FindingsMedium: medium,
            FindingsRetained: findings.Count,
            FindingsAutoFixed: 0,
            ScanTypes: scanTypes,
            NewSinceLast: 0,
            ResolvedSinceLast: 0,
            TopCategories: topCategories,
            CostUsd: costSummary?.TotalCost ?? 0m);
    }

    internal static List<string> DetermineScanTypes(PipelineContext pipeline)
    {
        var types = new List<string>();

        if (pipeline.Has(ContextKeys.StaticScanResult))
            types.Add("StaticPatternScan");
        if (pipeline.Has(ContextKeys.GitHistoryScanResult))
            types.Add("GitHistoryScan");
        if (pipeline.Has(ContextKeys.DependencyAuditResult))
            types.Add("DependencyAudit");
        if (pipeline.Has(ContextKeys.NucleiResult))
            types.Add("Nuclei");
        if (pipeline.Has(ContextKeys.SpectralResult))
            types.Add("Spectral");

        return types;
    }
}
