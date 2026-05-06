using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Scans git commit history for secrets that were committed and later deleted.
/// Stores findings in the pipeline context for downstream triage.
/// </summary>
public sealed class GitHistoryScanHandler(
    IGitHistoryScanner gitHistoryScanner,
    ISandboxFileReaderFactory readerFactory,
    ILogger<GitHistoryScanHandler> logger)
    : ICommandHandler<GitHistoryScanContext>
{
    public async Task<CommandResult> ExecuteAsync(
        GitHistoryScanContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo)
            || repo is null)
        {
            return CommandResult.Ok("No repository available, skipping git history scan");
        }

        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);
        var result = await gitHistoryScanner.ScanAsync(sandbox, reader, cancellationToken);
        context.Pipeline.Set(ContextKeys.GitHistoryScanResult, result);

        var critical = result.Findings.Count(f => !f.StillInWorkingTree);
        var high = result.Findings.Count(f => f.StillInWorkingTree);

        logger.LogInformation(
            "Git history scan complete: {Secrets} secrets in {Commits} commits ({Duration}ms) — {Critical} critical (history-only), {High} high",
            result.Findings.Count, result.CommitsScanned, result.DurationMilliseconds, critical, high);

        return CommandResult.Ok(
            $"Git history scan: {result.Findings.Count} secrets ({critical} critical, {high} high) in {result.CommitsScanned} commits");
    }
}
