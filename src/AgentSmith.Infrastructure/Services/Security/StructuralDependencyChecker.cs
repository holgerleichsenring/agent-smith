using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Performs structural checks on package.json (missing lockfile,
/// wildcard versions, insecure sources).
/// </summary>
internal sealed class StructuralDependencyChecker(ILogger logger)
{
    internal List<DependencyFinding> Check(string repoPath, string packageJsonPath)
    {
        var findings = new List<DependencyFinding>();

        try
        {
            var content = File.ReadAllText(packageJsonPath);

            CheckMissingLockfile(repoPath, findings);
            CheckWildcardVersions(content, findings);
            CheckInsecureSources(content, findings);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read package.json for structural checks");
        }

        return findings;
    }

    private static void CheckMissingLockfile(string repoPath, List<DependencyFinding> findings)
    {
        if (File.Exists(Path.Combine(repoPath, "package-lock.json"))
            || File.Exists(Path.Combine(repoPath, "yarn.lock"))
            || File.Exists(Path.Combine(repoPath, "pnpm-lock.yaml")))
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
