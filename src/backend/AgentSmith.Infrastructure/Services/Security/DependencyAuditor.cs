using System.Diagnostics;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Detects the package ecosystem in a repository and runs the appropriate
/// dependency audit tool (npm audit, pip-audit, dotnet list package) inside
/// the sandbox via Step{Kind=Run}. Returns null when no supported ecosystem.
/// </summary>
public sealed class DependencyAuditor(ILogger<DependencyAuditor> logger) : IDependencyAuditor
{
    private readonly AuditProcessRunner _processRunner = new(logger);
    private readonly NpmAuditParser _npmParser = new();
    private readonly PipAuditParser _pipParser = new();
    private readonly DotNetAuditParser _dotNetParser = new();
    private readonly StructuralDependencyChecker _structuralChecker = new(logger);

    public async Task<DependencyAuditResult?> AuditAsync(
        ISandbox sandbox, ISandboxFileReader reader, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var findings = new List<DependencyFinding>();
        var repoPath = Repository.SandboxWorkPath;

        var packageJsonPath = Path.Combine(repoPath, "package.json");
        if (await reader.ExistsAsync(packageJsonPath, cancellationToken))
            findings.AddRange(
                await _structuralChecker.CheckAsync(reader, repoPath, packageJsonPath, cancellationToken));

        var (ecosystem, auditFindings) = await DetectAndAuditAsync(sandbox, reader, repoPath, cancellationToken);

        if (ecosystem is null && findings.Count == 0)
        {
            logger.LogInformation("No supported package ecosystem detected at {RepoPath}", repoPath);
            return null;
        }

        if (auditFindings is not null)
            findings.AddRange(auditFindings);

        sw.Stop();
        return new DependencyAuditResult(findings, ecosystem ?? "structural", (int)sw.ElapsedMilliseconds);
    }

    private async Task<(string? Ecosystem, List<DependencyFinding>? Findings)> DetectAndAuditAsync(
        ISandbox sandbox, ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        if (await reader.ExistsAsync(Path.Combine(repoPath, "package-lock.json"), cancellationToken)
            || await reader.ExistsAsync(Path.Combine(repoPath, "package.json"), cancellationToken))
            return ("npm", await AuditNpmAsync(sandbox, cancellationToken));

        if (await reader.ExistsAsync(Path.Combine(repoPath, "requirements.txt"), cancellationToken)
            || await reader.ExistsAsync(Path.Combine(repoPath, "pyproject.toml"), cancellationToken))
            return ("python", await AuditPythonAsync(sandbox, reader, repoPath, cancellationToken));

        var entries = await reader.ListAsync(repoPath, maxDepth: 8, cancellationToken);
        if (entries.Any(e => e.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
            return ("dotnet", await AuditDotNetAsync(sandbox, cancellationToken));

        if (await reader.ExistsAsync(Path.Combine(repoPath, "go.mod"), cancellationToken))
            return ("go", []);

        return (null, null);
    }

    private async Task<List<DependencyFinding>?> AuditNpmAsync(
        ISandbox sandbox, CancellationToken cancellationToken)
    {
        var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
            sandbox, "npm audit --json", cancellationToken);

        if (exitCode < 0)
        {
            logger.LogWarning("npm not available or timed out: {Stderr}", stderr);
            return null;
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return [];

        try { return _npmParser.Parse(stdout); }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse npm audit JSON output");
            return null;
        }
    }

    private async Task<List<DependencyFinding>?> AuditPythonAsync(
        ISandbox sandbox, ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        var args = await reader.ExistsAsync(Path.Combine(repoPath, "requirements.txt"), cancellationToken)
            ? "--format=json --requirement=requirements.txt"
            : "--format=json";

        var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
            sandbox, $"pip-audit {args}", cancellationToken);

        if (exitCode < 0)
        {
            logger.LogInformation("pip-audit not found, attempting to install...");
            var (installExit, _, installErr) = await _processRunner.RunAsync(
                sandbox, "pip install pip-audit", cancellationToken);

            if (installExit != 0)
            {
                logger.LogWarning("Could not install pip-audit: {Stderr}", installErr);
                return null;
            }

            (exitCode, stdout, stderr) = await _processRunner.RunAsync(
                sandbox, $"pip-audit {args}", cancellationToken);

            if (exitCode < 0)
            {
                logger.LogWarning("pip-audit still not available after install: {Stderr}", stderr);
                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return [];

        try { return _pipParser.Parse(stdout); }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse pip-audit JSON output");
            return null;
        }
    }

    private async Task<List<DependencyFinding>?> AuditDotNetAsync(
        ISandbox sandbox, CancellationToken cancellationToken)
    {
        var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
            sandbox, "dotnet list package --vulnerable --format json", cancellationToken);

        if (exitCode < 0)
        {
            logger.LogWarning("dotnet CLI not available: {Stderr}", stderr);
            return null;
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return [];

        try { return _dotNetParser.Parse(stdout); }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse dotnet list package JSON output");
            return null;
        }
    }
}
