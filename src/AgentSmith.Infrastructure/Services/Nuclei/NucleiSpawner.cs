using System.Diagnostics;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Nuclei;

/// <summary>
/// Spawns a Nuclei Docker container to scan an API target.
/// Parses JSON-lines output into NucleiResult.
/// </summary>
public sealed class NucleiSpawner(ILogger<NucleiSpawner> logger) : INucleiScanner
{
    private const string NucleiImage = "projectdiscovery/nuclei:latest";
    private const int DefaultTimeoutSeconds = 300;

    public async Task<NucleiResult> ScanAsync(
        string targetUrl, string swaggerPath, CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"nuclei-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var swaggerDest = Path.Combine(tempDir, "swagger.json");
            File.Copy(swaggerPath, swaggerDest);

            var sw = Stopwatch.StartNew();

            var dockerTarget = RewriteLocalhostForDocker(targetUrl);

            var isLocalTarget = dockerTarget.Contains("host.docker.internal");
            var insecureFlag = isLocalTarget ? "-insecure " : "";

            var args = $"run --rm --add-host=host.docker.internal:host-gateway -v {tempDir}:/input {NucleiImage} " +
                       $"-target {dockerTarget} -jsonl " +
                       $"{insecureFlag}" +
                       $"-severity critical,high,medium " +
                       $"-tags api,owasp -exclude-tags dos";

            logger.LogInformation("Starting Nuclei scan: {Target} (docker: {DockerTarget})",
                targetUrl, dockerTarget);
            logger.LogDebug("Docker command: docker {Args}", args);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            sw.Stop();

            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
            {
                logger.LogWarning("Nuclei exited with code {Code}: {Error}", process.ExitCode, error);
            }

            var findings = ParseJsonLines(output);

            logger.LogInformation(
                "Nuclei scan completed: {Count} findings in {Duration}s",
                findings.Count, (int)sw.Elapsed.TotalSeconds);

            return new NucleiResult(findings, (int)sw.Elapsed.TotalSeconds, output);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    internal static string RewriteLocalhostForDocker(string url) =>
        url.Replace("://localhost", "://host.docker.internal")
           .Replace("://127.0.0.1", "://host.docker.internal");

    internal static List<NucleiFinding> ParseJsonLines(string output)
    {
        var findings = new List<NucleiFinding>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var templateId = root.TryGetProperty("template-id", out var tid)
                    ? tid.GetString() ?? "" : "";
                var name = root.TryGetProperty("info", out var info) && info.TryGetProperty("name", out var n)
                    ? n.GetString() ?? templateId : templateId;
                var severity = info.TryGetProperty("severity", out var sev)
                    ? sev.GetString() ?? "info" : "info";
                var matchedUrl = root.TryGetProperty("matched-at", out var url)
                    ? url.GetString() ?? "" : "";
                var description = info.TryGetProperty("description", out var desc)
                    ? desc.GetString() : null;
                var reference = info.TryGetProperty("reference", out var refArr) && refArr.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", refArr.EnumerateArray().Select(r => r.GetString()))
                    : null;

                findings.Add(new NucleiFinding(templateId, name, severity, matchedUrl, description, reference));
            }
            catch
            {
                // Skip non-JSON lines (Nuclei sometimes outputs status messages)
            }
        }

        return findings;
    }
}
