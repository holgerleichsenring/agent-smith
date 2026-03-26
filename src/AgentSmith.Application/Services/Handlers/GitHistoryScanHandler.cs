using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
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

        var result = await gitHistoryScanner.ScanAsync(repo.LocalPath, cancellationToken);
        context.Pipeline.Set(ContextKeys.GitHistoryScanResult, result);

        logger.LogInformation(
            "Git history scan complete: {Secrets} secrets found in {Commits} commits ({Duration}ms)",
            result.Findings.Count, result.CommitsScanned, result.DurationMilliseconds);

        return CommandResult.Ok(
            $"Git history scan: {result.Findings.Count} secrets found in {result.CommitsScanned} commits");
    }
}
