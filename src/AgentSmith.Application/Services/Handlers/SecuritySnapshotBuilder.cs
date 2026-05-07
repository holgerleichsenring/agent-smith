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
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var observations);
        var obs = (IReadOnlyList<SkillObservation>)(observations ?? []);

        pipeline.TryGet<RunCostSummary>(ContextKeys.RunCostSummary, out var costSummary);

        var high = obs.Count(o => o.Severity == ObservationSeverity.High);
        var medium = obs.Count(o => o.Severity == ObservationSeverity.Medium);

        var scanTypes = DetermineScanTypes(pipeline);

        var topCategories = obs
            .Where(o => !string.IsNullOrWhiteSpace(o.Category))
            .GroupBy(o => o.Category!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        return new SecurityRunSnapshot(
            Date: DateTimeOffset.UtcNow,
            Branch: repo.CurrentBranch.Value,
            FindingsCritical: 0,
            FindingsHigh: high,
            FindingsMedium: medium,
            FindingsRetained: obs.Count,
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
