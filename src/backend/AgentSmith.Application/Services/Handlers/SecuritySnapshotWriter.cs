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

        // p0277: count from the pre-merge RAW deterministic set when present (the
        // security-scan merge stashes it there) so the trend metric stays raw-vs-raw
        // across runs; fall back to SkillObservations when no merge ran (== raw anyway).
        if (ResolveCountBasis(context.Pipeline) is { Count: > 0 } observations)
        {
            var critical = observations.Count(o => o.Severity == ObservationSeverity.Critical);
            var high = observations.Count(o => o.Severity == ObservationSeverity.High);
            var medium = observations.Count(o => o.Severity == ObservationSeverity.Medium);

            snapshot = snapshot with
            {
                FindingsCritical = critical,
                FindingsHigh = high,
                FindingsMedium = medium,
                FindingsRetained = observations.Count,
            };

            logger.LogDebug(
                "Snapshot updated with observations: {Critical}C/{High}H/{Medium}M ({Total} total)",
                critical, high, medium, observations.Count);
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

    // p0277: prefer the pre-merge raw deterministic set (RawScannerObservations) for the
    // snapshot's finding counts; fall back to SkillObservations when no merge ran.
    private static List<SkillObservation>? ResolveCountBasis(PipelineContext pipeline) =>
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.RawScannerObservations, out var raw)
            && raw is not null
            ? raw
            : pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs)
                ? obs
                : null;

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
