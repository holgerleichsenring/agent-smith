using System.Globalization;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Reads previously committed security snapshots from .agentsmith/security/,
/// compares with the current scan results, and stores a SecurityTrend in the pipeline.
/// </summary>
public sealed class SecurityTrendHandler(
    ILogger<SecurityTrendHandler> logger)
    : ICommandHandler<SecurityTrendContext>
{
    private const string SecurityDir = ".agentsmith/security";

    public Task<CommandResult> ExecuteAsync(
        SecurityTrendContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo)
            || repo is null)
        {
            logger.LogInformation("No repository available, skipping security trend analysis");
            return Task.FromResult(CommandResult.Ok("No repository, skipping trend analysis"));
        }

        var currentSnapshot = BuildCurrentSnapshot(context.Pipeline, repo);

        var securityDir = Path.Combine(repo.LocalPath, SecurityDir);
        var previousSnapshots = LoadSnapshots(securityDir);

        var previous = previousSnapshots
            .OrderByDescending(s => s.Date)
            .FirstOrDefault();

        var trend = CalculateTrend(currentSnapshot, previous, previousSnapshots.Count);

        context.Pipeline.Set(ContextKeys.SecurityTrend, trend);

        logger.LogInformation(
            "Security trend: {New} new, {Resolved} resolved, critical delta {CriticalDelta}, high delta {HighDelta} (from {TotalScans} historical scans)",
            trend.NewFindings, trend.ResolvedFindings,
            trend.CriticalDelta, trend.HighDelta, trend.TotalScans);

        return Task.FromResult(CommandResult.Ok(
            $"Trend: {trend.NewFindings} new, {trend.ResolvedFindings} resolved"));
    }

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

    internal static SecurityTrend CalculateTrend(
        SecurityRunSnapshot current, SecurityRunSnapshot? previous, int totalScans)
    {
        if (previous is null)
        {
            return new SecurityTrend(
                NewFindings: current.FindingsRetained,
                ResolvedFindings: 0,
                CriticalDelta: current.FindingsCritical,
                HighDelta: current.FindingsHigh,
                TotalScans: totalScans + 1,
                AverageCost: current.CostUsd,
                Previous: null,
                Current: current);
        }

        var newFindings = Math.Max(0, current.FindingsRetained - previous.FindingsRetained + previous.FindingsAutoFixed);
        var resolvedFindings = Math.Max(0, previous.FindingsRetained - current.FindingsRetained + current.FindingsAutoFixed);

        var criticalDelta = current.FindingsCritical - previous.FindingsCritical;
        var highDelta = current.FindingsHigh - previous.FindingsHigh;

        var averageCost = totalScans > 0
            ? (previous.CostUsd * totalScans + current.CostUsd) / (totalScans + 1)
            : current.CostUsd;

        return new SecurityTrend(
            NewFindings: newFindings,
            ResolvedFindings: resolvedFindings,
            CriticalDelta: criticalDelta,
            HighDelta: highDelta,
            TotalScans: totalScans + 1,
            AverageCost: Math.Round(averageCost, 4),
            Previous: previous,
            Current: current);
    }

    internal static List<SecurityRunSnapshot> LoadSnapshots(string securityDir)
    {
        var snapshots = new List<SecurityRunSnapshot>();

        if (!Directory.Exists(securityDir))
            return snapshots;

        foreach (var file in Directory.GetFiles(securityDir, "*.yaml"))
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var snapshot = ParseSnapshotYaml(yaml);
                if (snapshot is not null)
                    snapshots.Add(snapshot);
            }
            catch
            {
                // Skip malformed snapshot files
            }
        }

        return snapshots;
    }

    internal static SecurityRunSnapshot? ParseSnapshotYaml(string yaml)
    {
        var lines = yaml.Split('\n');
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var scanTypes = new List<string>();
        var topCategories = new List<string>();
        string? currentList = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("  - ") && currentList is not null)
            {
                var item = line[4..].Trim();
                if (currentList == "scan_types") scanTypes.Add(item);
                else if (currentList == "top_categories") topCategories.Add(item);
                continue;
            }

            currentList = null;
            var colonIdx = line.IndexOf(':');
            if (colonIdx <= 0) continue;

            var key = line[..colonIdx].Trim();
            var value = line[(colonIdx + 1)..].Trim();

            if (string.IsNullOrEmpty(value))
            {
                if (key is "scan_types" or "top_categories")
                    currentList = key;
                continue;
            }

            values[key] = value;
        }

        if (!values.ContainsKey("date"))
            return null;

        return new SecurityRunSnapshot(
            Date: DateTimeOffset.TryParse(values.GetValueOrDefault("date", ""), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date : DateTimeOffset.MinValue,
            Branch: values.GetValueOrDefault("branch", "unknown"),
            FindingsCritical: ParseInt(values, "findings_critical"),
            FindingsHigh: ParseInt(values, "findings_high"),
            FindingsMedium: ParseInt(values, "findings_medium"),
            FindingsRetained: ParseInt(values, "findings_retained"),
            FindingsAutoFixed: ParseInt(values, "findings_auto_fixed"),
            ScanTypes: scanTypes,
            NewSinceLast: ParseInt(values, "new_since_last"),
            ResolvedSinceLast: ParseInt(values, "resolved_since_last"),
            TopCategories: topCategories,
            CostUsd: ParseDecimal(values, "cost_usd"));
    }

    private static int ParseInt(Dictionary<string, string> values, string key)
        => values.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : 0;

    private static decimal ParseDecimal(Dictionary<string, string> values, string key)
        => values.TryGetValue(key, out var v) && decimal.TryParse(v, CultureInfo.InvariantCulture, out var n) ? n : 0m;

    private static List<string> DetermineScanTypes(PipelineContext pipeline)
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
