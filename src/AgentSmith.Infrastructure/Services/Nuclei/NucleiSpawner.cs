using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Nuclei;

/// <summary>
/// Runs a Nuclei scan via IContainerRunner.
/// Works in any environment where containers can be spawned (local Docker, K8s, etc.).
/// </summary>
public sealed class NucleiSpawner(
    IContainerRunner containerRunner,
    ILogger<NucleiSpawner> logger) : INucleiScanner
{
    private const string NucleiImage = "projectdiscovery/nuclei:latest";

    public async Task<NucleiResult> ScanAsync(
        string targetUrl, string swaggerPath, CancellationToken cancellationToken)
    {
        var dockerTarget = RewriteLocalhostForDocker(targetUrl);
        var isLocal = dockerTarget.Contains("host.docker.internal");

        var tempDir = Path.Combine(Path.GetTempPath(), $"nuclei-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.Copy(swaggerPath, Path.Combine(tempDir, "swagger.json"));

            logger.LogInformation("Starting Nuclei scan: {Target} (container: {DockerTarget})",
                targetUrl, dockerTarget);

            // Extract endpoint URLs from swagger spec for Nuclei target list
            var endpointUrls = BuildEndpointUrls(swaggerPath, dockerTarget);
            var targetsFile = Path.Combine(tempDir, "targets.txt");
            await File.WriteAllTextAsync(targetsFile, string.Join("\n", endpointUrls), cancellationToken);

            logger.LogDebug("Generated {Count} target URLs from swagger spec", endpointUrls.Count);

            var command = new List<string>
            {
                "-list", "/input/targets.txt",
                "-jsonl",
                "-output", "/input/results.jsonl",
                "-severity", "critical,high,medium,low",
                "-tags", "exposure,misconfig,token,auth,cors,header,ssl,api",
                "-exclude-tags", "dos,fuzz",
                "-follow-redirects",
                "-no-interactsh",
                "-timeout", "10",
                "-retries", "1",
                "-no-mhe",
                "-concurrency", "10",
                "-rate-limit", "50",
            };

            var extraHosts = isLocal
                ? new Dictionary<string, string> { ["host.docker.internal"] = "host-gateway" }
                : null;

            var request = new ContainerRunRequest(
                NucleiImage,
                command,
                VolumeMounts: new Dictionary<string, string> { [tempDir] = "/input" },
                ExtraHosts: extraHosts,
                TimeoutSeconds: 600);

            var result = await containerRunner.RunAsync(request, cancellationToken);

            // Read from output file (survives timeout) with fallback to stdout
            var resultsFile = Path.Combine(tempDir, "results.jsonl");
            var output = File.Exists(resultsFile)
                ? await File.ReadAllTextAsync(resultsFile, cancellationToken)
                : result.Stdout;

            var findings = ParseJsonLines(output);

            logger.LogInformation(
                "Nuclei scan completed: {Count} findings in {Duration}s",
                findings.Count, result.DurationSeconds);

            if (!string.IsNullOrWhiteSpace(result.Stderr) && result.ExitCode != 0)
                logger.LogWarning("Nuclei stderr: {Stderr}", result.Stderr[..Math.Min(500, result.Stderr.Length)]);

            return new NucleiResult(findings, result.DurationSeconds, result.Stdout);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    internal static List<string> BuildEndpointUrls(string swaggerPath, string baseUrl)
    {
        var urls = new List<string> { baseUrl };

        try
        {
            var json = File.ReadAllText(swaggerPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                var trimmedBase = baseUrl.TrimEnd('/');
                foreach (var path in paths.EnumerateObject())
                {
                    // Replace path parameters with sample values
                    var endpointPath = path.Name
                        .Replace("{id}", "1")
                        .Replace("{Id}", "1");

                    urls.Add($"{trimmedBase}{endpointPath}");
                }
            }
        }
        catch
        {
            // If swagger parsing fails, scan base URL only
        }

        return urls.Distinct().ToList();
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
                // Skip non-JSON lines (Nuclei status messages)
            }
        }

        return findings;
    }
}
