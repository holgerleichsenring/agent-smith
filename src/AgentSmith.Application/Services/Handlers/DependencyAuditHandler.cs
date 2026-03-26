using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
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
    ILogger<DependencyAuditHandler> logger)
    : ICommandHandler<DependencyAuditContext>
{
    public async Task<CommandResult> ExecuteAsync(
        DependencyAuditContext context, CancellationToken cancellationToken)
    {
        var repo = context.Pipeline.Get<Repository>(ContextKeys.Repository);
        var repoPath = repo.LocalPath;

        logger.LogInformation("Starting dependency audit for {RepoPath}", repoPath);

        var result = await dependencyAuditor.AuditAsync(repoPath, cancellationToken);

        if (result is null)
        {
            logger.LogInformation("No supported package manager found, skipping dependency audit");
            return CommandResult.Ok("No supported package manager found, skipping");
        }

        context.Pipeline.Set(ContextKeys.DependencyAuditResult, result);

        logger.LogInformation(
            "Dependency audit ({Ecosystem}): {Count} vulnerabilities found in {Duration}ms",
            result.Ecosystem, result.Findings.Count, result.DurationMilliseconds);

        return CommandResult.Ok(
            $"Dependency audit ({result.Ecosystem}): {result.Findings.Count} vulnerabilities found");
    }
}
