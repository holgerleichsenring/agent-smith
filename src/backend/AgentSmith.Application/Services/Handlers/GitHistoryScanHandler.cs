using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
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

        var observations = result.Findings.Select(f => new SkillObservation(
            Id: 0, Role: "git-history-scanner",
            Concern: ObservationConcern.Security,
            Description: $"Secret in commit {(f.CommitHash.Length >= 7 ? f.CommitHash[..7] : f.CommitHash)} [{(f.StillInWorkingTree ? "still in working tree" : "removed")}]: {f.Title}",
            Suggestion: f.RevokeUrl is null ? "" : $"Rotate the credential and revoke it via {f.RevokeUrl}.",
            Blocking: false,
            Severity: f.StillInWorkingTree ? ObservationSeverity.High : ObservationSeverity.Medium,
            Confidence: 90,
            Rationale: f.Description,
            File: f.File, StartLine: f.Line,
            EvidenceMode: EvidenceMode.AnalyzedFromSource,
            Category: "secrets")).ToList();
        ScannerObservationFactory.AppendObservations(context.Pipeline, observations);

        var critical = result.Findings.Count(f => !f.StillInWorkingTree);
        var high = result.Findings.Count(f => f.StillInWorkingTree);

        logger.LogInformation(
            "Git history scan complete: {Secrets} secrets in {Commits} commits ({Duration}ms) — {Critical} critical (history-only), {High} high",
            result.Findings.Count, result.CommitsScanned, result.DurationMilliseconds, critical, high);

        return CommandResult.Ok(
            $"Git history scan: {result.Findings.Count} secrets ({critical} critical, {high} high) in {result.CommitsScanned} commits");
    }
}
