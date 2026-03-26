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
    private const int ProcessTimeoutSeconds = 60;

    public async Task<DependencyAuditResult?> AuditAsync(string repoPath, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var findings = new List<DependencyFinding>();

        // Always run structural checks against package.json if present
        var packageJsonPath = Path.Combine(repoPath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            findings.AddRange(RunStructuralChecks(repoPath, packageJsonPath));
        }

        // Detect ecosystem and run audit tool
        var (ecosystem, auditFindings) = await DetectAndAuditAsync(repoPath, cancellationToken);

        if (ecosystem is null && findings.Count == 0)
        {
            logger.LogInformation("No supported package ecosystem detected in {RepoPath}", repoPath);
            return null;
        }

        if (auditFindings is not null)
        {
            findings.AddRange(auditFindings);
        }

        sw.Stop();
        var effectiveEcosystem = ecosystem ?? "structural";

        return new DependencyAuditResult(findings, effectiveEcosystem, (int)sw.ElapsedMilliseconds);
    }

    private async Task<(string? Ecosystem, List<DependencyFinding>? Findings)> DetectAndAuditAsync(
        string repoPath, CancellationToken cancellationToken)
    {
        // 1. npm
        if (File.Exists(Path.Combine(repoPath, "package-lock.json"))
            || File.Exists(Path.Combine(repoPath, "package.json")))
        {
            var findings = await AuditNpmAsync(repoPath, cancellationToken);
            return findings is not null ? ("npm", findings) : ("npm", null);
        }

        // 2. Python
        if (File.Exists(Path.Combine(repoPath, "requirements.txt"))
            || File.Exists(Path.Combine(repoPath, "pyproject.toml")))
        {
            var findings = await AuditPythonAsync(repoPath, cancellationToken);
            return findings is not null ? ("python", findings) : ("python", null);
        }

        // 3. .NET
        if (Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories).Length > 0)
        {
            var findings = await AuditDotNetAsync(repoPath, cancellationToken);
            return findings is not null ? ("dotnet", findings) : ("dotnet", null);
        }

        // 4. Go
        if (File.Exists(Path.Combine(repoPath, "go.mod")))
        {
            return ("go", []);
        }

        return (null, null);
    }

    private async Task<List<DependencyFinding>?> AuditNpmAsync(
        string repoPath, CancellationToken cancellationToken)
    {
        var (exitCode, stdout, stderr) = await RunProcessAsync(
            "npm", "audit --json", repoPath, cancellationToken);

        if (exitCode < 0)
        {
            logger.LogWarning("npm not available or timed out: {Stderr}", stderr);
            return null;
        }

        // npm audit returns exit code > 0 when vulnerabilities are found, which is expected
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return [];
        }

        try
        {
            return ParseNpmAuditOutput(stdout);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse npm audit JSON output");
            return null;
        }
    }

    private static List<DependencyFinding> ParseNpmAuditOutput(string json)
    {
        var findings = new List<DependencyFinding>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("vulnerabilities", out var vulns))
            return findings;

        foreach (var vuln in vulns.EnumerateObject())
        {
            var packageName = vuln.Name;
            var detail = vuln.Value;

            var severity = detail.TryGetProperty("severity", out var sev)
                ? sev.GetString() ?? "unknown"
                : "unknown";

            var fixAvailable = detail.TryGetProperty("fixAvailable", out var fix)
                && fix.ValueKind == JsonValueKind.True;

            var title = $"Vulnerable dependency: {packageName}";
            var description = fixAvailable ? "Fix available" : "No fix available";

            // Extract CVE from via array if present
            string? cve = null;
            if (detail.TryGetProperty("via", out var via) && via.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in via.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object
                        && entry.TryGetProperty("url", out var url))
                    {
                        var urlStr = url.GetString();
                        if (urlStr?.Contains("CVE-", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            cve = ExtractCve(urlStr);
                            break;
                        }
                    }

                    if (entry.ValueKind == JsonValueKind.Object
                        && entry.TryGetProperty("title", out var titleProp))
                    {
                        title = titleProp.GetString() ?? title;
                    }
                }
            }

            findings.Add(new DependencyFinding(
                Package: packageName,
                Version: "current",
                Severity: severity,
                Cve: cve,
                Title: title,
                Description: description,
                FixVersion: fixAvailable ? "latest" : null,
                Ecosystem: "npm"));
        }

        return findings;
    }

    private async Task<List<DependencyFinding>?> AuditPythonAsync(
        string repoPath, CancellationToken cancellationToken)
    {
        var args = File.Exists(Path.Combine(repoPath, "requirements.txt"))
            ? "--format=json --requirement=requirements.txt"
            : "--format=json";

        var (exitCode, stdout, stderr) = await RunProcessAsync(
            "pip-audit", args, repoPath, cancellationToken);

        if (exitCode < 0)
        {
            // pip-audit not installed, try installing it
            logger.LogInformation("pip-audit not found, attempting to install...");
            var (installExit, _, installErr) = await RunProcessAsync(
                "pip", "install pip-audit", repoPath, cancellationToken);

            if (installExit != 0)
            {
                logger.LogWarning("Could not install pip-audit: {Stderr}", installErr);
                return null;
            }

            (exitCode, stdout, stderr) = await RunProcessAsync(
                "pip-audit", args, repoPath, cancellationToken);

            if (exitCode < 0)
            {
                logger.LogWarning("pip-audit still not available after install: {Stderr}", stderr);
                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return [];
        }

        try
        {
            return ParsePipAuditOutput(stdout);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse pip-audit JSON output");
            return null;
        }
    }

    private static List<DependencyFinding> ParsePipAuditOutput(string json)
    {
        var findings = new List<DependencyFinding>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            return findings;

        foreach (var entry in root.EnumerateArray())
        {
            var name = entry.TryGetProperty("name", out var n) ? n.GetString() ?? "unknown" : "unknown";
            var version = entry.TryGetProperty("version", out var v) ? v.GetString() ?? "unknown" : "unknown";

            if (!entry.TryGetProperty("vulns", out var vulns) || vulns.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var vuln in vulns.EnumerateArray())
            {
                var id = vuln.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                var desc = vuln.TryGetProperty("description", out var descProp)
                    ? descProp.GetString() ?? "No description"
                    : "No description";

                string? fixVersion = null;
                if (vuln.TryGetProperty("fix_versions", out var fixes) && fixes.ValueKind == JsonValueKind.Array)
                {
                    var versions = new List<string>();
                    foreach (var fv in fixes.EnumerateArray())
                    {
                        var s = fv.GetString();
                        if (s is not null) versions.Add(s);
                    }

                    if (versions.Count > 0)
                        fixVersion = string.Join(", ", versions);
                }

                findings.Add(new DependencyFinding(
                    Package: name,
                    Version: version,
                    Severity: "unknown",
                    Cve: id,
                    Title: $"Vulnerability in {name} {version}",
                    Description: desc,
                    FixVersion: fixVersion,
                    Ecosystem: "python"));
            }
        }

        return findings;
    }

    private async Task<List<DependencyFinding>?> AuditDotNetAsync(
        string repoPath, CancellationToken cancellationToken)
    {
        var (exitCode, stdout, stderr) = await RunProcessAsync(
            "dotnet", "list package --vulnerable --format json", repoPath, cancellationToken);

        if (exitCode < 0)
        {
            logger.LogWarning("dotnet CLI not available: {Stderr}", stderr);
            return null;
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return [];
        }

        try
        {
            return ParseDotNetAuditOutput(stdout);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse dotnet list package JSON output");
            return null;
        }
    }

    private static List<DependencyFinding> ParseDotNetAuditOutput(string json)
    {
        var findings = new List<DependencyFinding>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("projects", out var projects))
            return findings;

        foreach (var project in projects.EnumerateArray())
        {
            if (!project.TryGetProperty("frameworks", out var frameworks))
                continue;

            foreach (var framework in frameworks.EnumerateArray())
            {
                if (!framework.TryGetProperty("topLevelPackages", out var packages))
                    continue;

                foreach (var package in packages.EnumerateArray())
                {
                    var name = package.TryGetProperty("id", out var id) ? id.GetString() ?? "unknown" : "unknown";
                    var version = package.TryGetProperty("resolvedVersion", out var ver)
                        ? ver.GetString() ?? "unknown"
                        : "unknown";

                    if (!package.TryGetProperty("vulnerabilities", out var vulns))
                        continue;

                    foreach (var vuln in vulns.EnumerateArray())
                    {
                        var severity = vuln.TryGetProperty("severity", out var sev)
                            ? sev.GetString() ?? "unknown"
                            : "unknown";
                        var advisoryUrl = vuln.TryGetProperty("advisoryurl", out var url)
                            ? url.GetString()
                            : null;

                        findings.Add(new DependencyFinding(
                            Package: name,
                            Version: version,
                            Severity: severity,
                            Cve: advisoryUrl is not null ? ExtractCve(advisoryUrl) : null,
                            Title: $"Vulnerable package: {name} {version}",
                            Description: advisoryUrl ?? "See advisory for details",
                            FixVersion: null,
                            Ecosystem: "dotnet"));
                    }
                }
            }
        }

        return findings;
    }

    private List<DependencyFinding> RunStructuralChecks(string repoPath, string packageJsonPath)
    {
        var findings = new List<DependencyFinding>();

        try
        {
            var content = File.ReadAllText(packageJsonPath);

            // Check for missing lockfile
            if (!File.Exists(Path.Combine(repoPath, "package-lock.json"))
                && !File.Exists(Path.Combine(repoPath, "yarn.lock"))
                && !File.Exists(Path.Combine(repoPath, "pnpm-lock.yaml")))
            {
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

            // Check for wildcard versions
            if (content.Contains("\"*\"", StringComparison.Ordinal))
            {
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

            // Check for git:// or http:// dependencies
            if (content.Contains("git://", StringComparison.OrdinalIgnoreCase)
                || content.Contains("http://", StringComparison.OrdinalIgnoreCase))
            {
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
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read package.json for structural checks");
        }

        return findings;
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
        string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(ProcessTimeoutSeconds));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                logger.LogWarning("{FileName} timed out after {Timeout}s", fileName, ProcessTimeoutSeconds);
                return (-1, string.Empty, "Process timed out");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return (process.ExitCode, stdout, stderr);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // Tool not installed
            logger.LogDebug(ex, "{FileName} not found on PATH", fileName);
            return (-1, string.Empty, $"{fileName} not found: {ex.Message}");
        }
    }

    private static string? ExtractCve(string text)
    {
        var idx = text.IndexOf("CVE-", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var end = idx + 4; // past "CVE-"
        while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '-'))
            end++;

        var cve = text[idx..end];
        return cve.Length > 4 ? cve : null;
    }
}
