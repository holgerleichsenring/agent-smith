using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Runs static regex-based pattern scanning against repository source files.
/// Stores the result in the pipeline context for downstream triage.
/// </summary>
public sealed class StaticPatternScanHandler(
    IStaticPatternScanner scanner,
    ILogger<StaticPatternScanHandler> logger)
    : ICommandHandler<StaticPatternScanContext>
{
    public async Task<CommandResult> ExecuteAsync(
        StaticPatternScanContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo)
            || repo is null)
        {
            logger.LogInformation("No repository available, skipping static pattern scan");
            return CommandResult.Ok("No repository available, skipping static scan");
        }

        var result = await scanner.ScanAsync(repo.LocalPath, cancellationToken);
        context.Pipeline.Set(ContextKeys.StaticScanResult, result);

        var findings = result.Findings;
        var critical = findings.Count(f => f.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase));
        var high = findings.Count(f => f.Severity.Equals("high", StringComparison.OrdinalIgnoreCase));
        var medium = findings.Count(f => f.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase));
        var low = findings.Count - critical - high - medium;

        logger.LogInformation(
            "Static scan complete: {Findings} findings in {Files} files ({Patterns} patterns) in {Duration}ms — {Critical}C/{High}H/{Medium}M/{Low}L",
            findings.Count, result.FilesScanned, result.PatternsApplied, result.DurationMilliseconds,
            critical, high, medium, low);

        return CommandResult.Ok(
            $"Static scan: {findings.Count} findings ({critical}C/{high}H/{medium}M/{low}L) in {result.FilesScanned} files");
    }
}
