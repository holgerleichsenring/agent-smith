using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Performs structural checks on package.json (missing lockfile,
/// wildcard versions, insecure sources). Reads via ISandboxFileReader.
/// </summary>
internal sealed class StructuralDependencyChecker(ILogger logger)
{
    internal async Task<List<DependencyFinding>> CheckAsync(
        ISandboxFileReader reader, string repoPath, string packageJsonPath, CancellationToken cancellationToken)
    {
        var findings = new List<DependencyFinding>();

        try
        {
            var content = await reader.TryReadAsync(packageJsonPath, cancellationToken);
            if (content is null) return findings;

            await CheckMissingLockfileAsync(reader, repoPath, findings, cancellationToken);
            CheckWildcardVersions(content, findings);
            CheckInsecureSources(content, findings);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read package.json for structural checks");
        }

        return findings;
    }

    private static async Task CheckMissingLockfileAsync(
        ISandboxFileReader reader, string repoPath, List<DependencyFinding> findings, CancellationToken cancellationToken)
    {
        if (await reader.ExistsAsync(Path.Combine(repoPath, "package-lock.json"), cancellationToken)
            || await reader.ExistsAsync(Path.Combine(repoPath, "yarn.lock"), cancellationToken)
            || await reader.ExistsAsync(Path.Combine(repoPath, "pnpm-lock.yaml"), cancellationToken))
            return;

        findings.Add(new DependencyFinding(
            Package: "package.json",
            Version: "N/A",
            Severity: "medium",
            Cve: null,
            Title: "Missing lockfile",
            Description: "No package-lock.json, yarn.lock, or pnpm-lock.yaml found. " +
                         "Without a lockfile, dependency versions are not deterministic.",
            FixVersion: null,
            Ecosystem: "structural"));
    }

    private static void CheckWildcardVersions(string content, List<DependencyFinding> findings)
    {
        if (!content.Contains("\"*\"", StringComparison.Ordinal))
            return;

        findings.Add(new DependencyFinding(
            Package: "package.json",
            Version: "N/A",
            Severity: "high",
            Cve: null,
            Title: "Wildcard dependency version",
            Description: "One or more dependencies use \"*\" as version, " +
                         "which allows any version including potentially malicious ones.",
            FixVersion: null,
            Ecosystem: "structural"));
    }

    private static void CheckInsecureSources(string content, List<DependencyFinding> findings)
    {
        if (!content.Contains("git://", StringComparison.OrdinalIgnoreCase)
            && !content.Contains("http://", StringComparison.OrdinalIgnoreCase))
            return;

        findings.Add(new DependencyFinding(
            Package: "package.json",
            Version: "N/A",
            Severity: "high",
            Cve: null,
            Title: "Insecure dependency source",
            Description: "Dependencies reference git:// or http:// URLs which are vulnerable " +
                         "to man-in-the-middle attacks. Use https:// instead.",
            FixVersion: null,
            Ecosystem: "structural"));
    }
}
