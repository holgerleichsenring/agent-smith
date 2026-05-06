using System.Globalization;
using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Writes a SecurityRunSnapshot as YAML to .agentsmith/security/{date}-{branch}.yaml.
/// Called after CompileFindings to persist the current scan's snapshot for trend analysis.
/// </summary>
public sealed class SecuritySnapshotWriter(
    ISandboxFileReaderFactory readerFactory,
    ILogger<SecuritySnapshotWriter> logger)
    : ICommandHandler<SecuritySnapshotWriteContext>
{
    private const string SecurityDir = ".agentsmith/security";

    public async Task<CommandResult> ExecuteAsync(
        SecuritySnapshotWriteContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo)
            || repo is null)
        {
            logger.LogInformation("No repository available, skipping snapshot write");
            return CommandResult.Ok("No repository, skipping snapshot write");
        }

        if (!context.Pipeline.TryGet<SecurityTrend>(ContextKeys.SecurityTrend, out var trend)
            || trend is null)
        {
            logger.LogInformation("No security trend data, skipping snapshot write");
            return CommandResult.Ok("No trend data, skipping snapshot write");
        }

        var snapshot = trend.Current;

        if (context.Pipeline.TryGet<IReadOnlyList<Finding>>(
                ContextKeys.ExtractedFindings, out var gateFindings) && gateFindings is { Count: > 0 })
        {
            var critical = gateFindings.Count(f => f.Severity.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase));
            var high = gateFindings.Count(f => f.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase));
            var medium = gateFindings.Count(f => f.Severity.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase));

            snapshot = snapshot with
            {
                FindingsCritical = critical,
                FindingsHigh = high,
                FindingsMedium = medium,
                FindingsRetained = gateFindings.Count,
            };

            logger.LogDebug(
                "Snapshot updated with gate-filtered findings: {Critical}C/{High}H/{Medium}M ({Total} total)",
                critical, high, medium, gateFindings.Count);
        }

        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);

        var securityDir = Path.Combine(repo.LocalPath, SecurityDir);
        var fileName = $"{snapshot.Date:yyyy-MM-dd}-{SanitizeBranch(snapshot.Branch)}.yaml";
        var filePath = Path.Combine(securityDir, fileName);

        var yaml = FormatSnapshot(snapshot);
        await reader.WriteAsync(filePath, yaml, cancellationToken);

        logger.LogInformation(
            "Written security snapshot to {Path} ({Critical}C/{High}H/{Medium}M, {Total} findings)",
            fileName, snapshot.FindingsCritical, snapshot.FindingsHigh,
            snapshot.FindingsMedium, snapshot.FindingsRetained);

        return CommandResult.Ok($"Snapshot written to {fileName}");
    }

    internal static string FormatSnapshot(SecurityRunSnapshot snapshot)
    {
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.AppendLine($"date: {snapshot.Date:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine($"branch: {snapshot.Branch}");
        sb.AppendLine($"findings_critical: {snapshot.FindingsCritical}");
        sb.AppendLine($"findings_high: {snapshot.FindingsHigh}");
        sb.AppendLine($"findings_medium: {snapshot.FindingsMedium}");
        sb.AppendLine($"findings_retained: {snapshot.FindingsRetained}");
        sb.AppendLine($"findings_auto_fixed: {snapshot.FindingsAutoFixed}");

        sb.AppendLine("scan_types:");
        foreach (var scanType in snapshot.ScanTypes)
            sb.AppendLine($"  - {scanType}");

        sb.AppendLine($"new_since_last: {snapshot.NewSinceLast}");
        sb.AppendLine($"resolved_since_last: {snapshot.ResolvedSinceLast}");

        sb.AppendLine("top_categories:");
        foreach (var category in snapshot.TopCategories)
            sb.AppendLine($"  - {category}");

        sb.AppendLine(string.Format(ci, "cost_usd: {0:F4}", snapshot.CostUsd));

        return sb.ToString();
    }

    internal static string SanitizeBranch(string branch)
    {
        var sanitized = branch
            .Replace('/', '-')
            .Replace('\\', '-')
            .Replace(' ', '-')
            .ToLowerInvariant();

        return sanitized.Length > 40 ? sanitized[..40].TrimEnd('-') : sanitized;
    }
}
