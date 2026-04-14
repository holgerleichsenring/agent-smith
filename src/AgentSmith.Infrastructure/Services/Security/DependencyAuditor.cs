using System.Diagnostics;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Detects the package ecosystem in a repository and runs the appropriate
/// dependency audit tool (npm audit, pip-audit, dotnet list package).
/// Returns null when no supported ecosystem is detected.
/// </summary>
public sealed class DependencyAuditor(ILogger<DependencyAuditor> logger) : IDependencyAuditor
{
    private readonly AuditProcessRunner _processRunner = new(logger);
    private readonly NpmAuditParser _npmParser = new();
    private readonly PipAuditParser _pipParser = new();
    private readonly DotNetAuditParser _dotNetParser = new();
    private readonly StructuralDependencyChecker _structuralChecker = new(logger);

    public async Task<DependencyAuditResult?> AuditAsync(string repoPath, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var findings = new List<DependencyFinding>();

        var packageJsonPath = Path.Combine(repoPath, "package.json");
        if (File.Exists(packageJsonPath))
            findings.AddRange(_structuralChecker.Check(repoPath, packageJsonPath));

        var (ecosystem, auditFindings) = await DetectAndAuditAsync(repoPath, cancellationToken);

        if (ecosystem is null && findings.Count == 0)
        {
            logger.LogInformation("No supported package ecosystem detected in {RepoPath}", repoPath);
            return null;
        }

        if (auditFindings is not null)
            findings.AddRange(auditFindings);

        sw.Stop();
        return new DependencyAuditResult(findings, ecosystem ?? "structural", (int)sw.ElapsedMilliseconds);
    }

    private async Task<(string? Ecosystem, List<DependencyFinding>? Findings)> DetectAndAuditAsync(
        string repoPath, CancellationToken cancellationToken)
    {
        if (File.Exists(Path.Combine(repoPath, "package-lock.json"))
            || File.Exists(Path.Combine(repoPath, "package.json")))
            return ("npm", await AuditNpmAsync(repoPath, cancellationToken));

        if (File.Exists(Path.Combine(repoPath, "requirements.txt"))
            || File.Exists(Path.Combine(repoPath, "pyproject.toml")))
            return ("python", await AuditPythonAsync(repoPath, cancellationToken));

        if (Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories).Length > 0)
            return ("dotnet", await AuditDotNetAsync(repoPath, cancellationToken));

        if (File.Exists(Path.Combine(repoPath, "go.mod")))
            return ("go", []);

        return (null, null);
    }

    private async Task<List<DependencyFinding>?> AuditNpmAsync(
        string repoPath, CancellationToken cancellationToken)
    {
        var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
            "npm", "audit --json", repoPath, cancellationToken);

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
        string repoPath, CancellationToken cancellationToken)
    {
        var args = File.Exists(Path.Combine(repoPath, "requirements.txt"))
            ? "--format=json --requirement=requirements.txt"
            : "--format=json";

        var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
            "pip-audit", args, repoPath, cancellationToken);

        if (exitCode < 0)
        {
            logger.LogInformation("pip-audit not found, attempting to install...");
            var (installExit, _, installErr) = await _processRunner.RunAsync(
                "pip", "install pip-audit", repoPath, cancellationToken);

            if (installExit != 0)
            {
                logger.LogWarning("Could not install pip-audit: {Stderr}", installErr);
                return null;
            }

            (exitCode, stdout, stderr) = await _processRunner.RunAsync(
                "pip-audit", args, repoPath, cancellationToken);

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
        string repoPath, CancellationToken cancellationToken)
    {
        var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
            "dotnet", "list package --vulnerable --format json", repoPath, cancellationToken);

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
