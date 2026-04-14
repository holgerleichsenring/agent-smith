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

        var currentSnapshot = SecuritySnapshotBuilder.BuildCurrentSnapshot(context.Pipeline, repo);

        var securityDir = Path.Combine(repo.LocalPath, SecurityDir);
        var previousSnapshots = SnapshotYamlParser.LoadSnapshots(securityDir);

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
}
