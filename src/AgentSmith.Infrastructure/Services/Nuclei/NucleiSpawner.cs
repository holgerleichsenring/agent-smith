using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Nuclei;

/// <summary>
/// Runs a Nuclei scan via IToolRunner.
/// Configuration loaded from config/nuclei.yaml.
/// </summary>
public sealed class NucleiSpawner(
    IToolRunner toolRunner,
    NucleiConfig config,
    ILogger<NucleiSpawner> logger) : INucleiScanner
{
    public async Task<NucleiResult> ScanAsync(
        string targetUrl, string swaggerPath, CancellationToken cancellationToken)
    {
        var dockerTarget = RewriteLocalhostForDocker(targetUrl);
        var isLocal = dockerTarget.Contains("host.docker.internal");

        logger.LogInformation("Starting Nuclei scan: {Target} (container: {DockerTarget})",
            targetUrl, dockerTarget);

        // Build target list from swagger endpoints
        var endpointUrls = BuildEndpointUrls(swaggerPath, dockerTarget);
        logger.LogDebug("Generated {Count} target URLs from swagger spec", endpointUrls.Count);

        var inputFiles = new Dictionary<string, string>
        {
            ["swagger.json"] = File.ReadAllText(swaggerPath),
            ["targets.txt"] = string.Join("\n", endpointUrls),
        };

        var arguments = new List<string>
        {
            "-list", "{work}/targets.txt",
            "-jsonl",
            "-output", "{work}/results.jsonl",
            "-severity", config.Severity,
            "-tags", config.Tags,
            "-exclude-tags", config.ExcludeTags,
            "-follow-redirects",
            "-no-interactsh",
            "-timeout", config.Timeout.ToString(),
            "-retries", config.Retries.ToString(),
            "-no-mhe",
            "-concurrency", config.Concurrency.ToString(),
            "-rate-limit", config.RateLimit.ToString(),
        };

        var extraHosts = isLocal
            ? new Dictionary<string, string> { ["host.docker.internal"] = "host-gateway" }
            : null;

        var request = new ToolRunRequest(
            "nuclei", arguments, inputFiles,
            OutputFileName: "results.jsonl",
            ExtraHosts: extraHosts,
            TimeoutSeconds: config.ContainerTimeout);

        var result = await toolRunner.RunAsync(request, cancellationToken);

        var output = result.OutputFileContent ?? result.Stdout;
        var findings = ParseJsonLines(output);

        logger.LogInformation(
            "Nuclei scan completed: {Count} findings in {Duration}s",
            findings.Count, result.DurationSeconds);

        if (!string.IsNullOrWhiteSpace(result.Stderr) && result.ExitCode != 0)
            logger.LogWarning("Nuclei stderr: {Stderr}", result.Stderr[..Math.Min(500, result.Stderr.Length)]);

        return new NucleiResult(findings, result.DurationSeconds, result.Stdout);
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
