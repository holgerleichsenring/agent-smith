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
/// Runs dependency vulnerability auditing against the checked-out repository.
/// Detects the package ecosystem and delegates to <see cref="IDependencyAuditor"/>.
/// </summary>
public sealed class DependencyAuditHandler(
    IDependencyAuditor dependencyAuditor,
    ISandboxFileReaderFactory readerFactory,
    ILogger<DependencyAuditHandler> logger)
    : ICommandHandler<DependencyAuditContext>
{
    public async Task<CommandResult> ExecuteAsync(
        DependencyAuditContext context, CancellationToken cancellationToken)
    {
        var repo = context.Pipeline.Get<Repository>(ContextKeys.Repository);

        logger.LogInformation("Starting dependency audit for {RepoPath}", repo.LocalPath);

        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);
        var result = await dependencyAuditor.AuditAsync(sandbox, reader, cancellationToken);

        if (result is null)
        {
            logger.LogInformation("No supported package manager found, skipping dependency audit");
            return CommandResult.Ok("No supported package manager found, skipping");
        }

        context.Pipeline.Set(ContextKeys.DependencyAuditResult, result);

        var observations = result.Findings.Select(f => new SkillObservation(
            Id: 0, Role: "dependency-auditor",
            Concern: ObservationConcern.Security,
            Description: $"{result.Ecosystem.ToLowerInvariant()} package {f.Package}@{f.Version}: {f.Title}{(f.Cve is null ? "" : $" [{f.Cve}]")}",
            Suggestion: f.FixVersion is null ? "" : $"Upgrade to {f.FixVersion}.",
            Blocking: false,
            Severity: ScannerObservationFactory.ParseSeverity(f.Severity, logger),
            Confidence: 80,
            Rationale: f.Description,
            EvidenceMode: EvidenceMode.AnalyzedFromSource,
            Category: "dependencies")).ToList();
        ScannerObservationFactory.AppendObservations(context.Pipeline, observations);

        logger.LogInformation(
            "Dependency audit ({Ecosystem}): {Count} vulnerabilities found in {Duration}ms",
            result.Ecosystem, result.Findings.Count, result.DurationMilliseconds);

        return CommandResult.Ok(
            $"Dependency audit ({result.Ecosystem}): {result.Findings.Count} vulnerabilities found");
    }
}
