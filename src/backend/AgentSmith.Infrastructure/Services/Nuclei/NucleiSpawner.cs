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
    ToolRunnerConfig toolRunnerConfig,
    ILogger<NucleiSpawner> logger) : INucleiScanner
{
    public async Task<NucleiResult> ScanAsync(
        string targetUrl, string swaggerPath, CancellationToken cancellationToken)
    {
        var dockerHostname = toolRunnerConfig.DockerHostname;
        var dockerTarget = RewriteLocalhostForDocker(targetUrl, dockerHostname);
        var isLocal = dockerTarget.Contains(dockerHostname);

        logger.LogInformation("Starting Nuclei scan: {Target} (container: {DockerTarget})",
            targetUrl, dockerTarget);

        // Build target list from swagger endpoints
        var endpointUrls = BuildEndpointUrls(swaggerPath, dockerTarget, logger, out var degradedReason);
        logger.LogDebug("Generated {Count} target URLs from swagger spec", endpointUrls.Count);

        var inputFiles = new Dictionary<string, string>
        {
            ["swagger.json"] = File.ReadAllText(swaggerPath),
            ["targets.txt"] = string.Join("\n", endpointUrls),
        };

        var extraHosts = isLocal
            ? new Dictionary<string, string> { [dockerHostname] = "host-gateway" }
            : null;

        var request = new ToolRunRequest(
            "nuclei", NucleiArgumentBuilder.BuildArguments(config), inputFiles,
            OutputFileName: "results.jsonl",
            ExtraHosts: extraHosts,
            TimeoutSeconds: config.ContainerTimeout);

        var result = await toolRunner.RunAsync(request, cancellationToken);

        var output = result.OutputFileContent ?? result.Stdout;
        var findings = NucleiFindingReader.ParseJsonLines(output);

        if (!string.IsNullOrWhiteSpace(result.Stderr) && result.ExitCode != 0)
            logger.LogWarning("Nuclei stderr: {Stderr}", result.Stderr[..Math.Min(500, result.Stderr.Length)]);

        var scan = NucleiScanOutcome.From(result, findings, degradedReason, config.ContainerTimeout);
        LogOutcome(scan, result);
        return scan;
    }

    /// <summary>
    /// 2026-08-30-26ed: a scan the runner cut off is not a scan that completed, and the
    /// log said "completed" for both — beside the very timeout that had produced the count.
    /// </summary>
    private void LogOutcome(NucleiResult scan, ToolResult result)
    {
        if (result.CutOff)
            logger.LogWarning("Nuclei scan {Reason}: {Count} findings from the part it reached",
                scan.DegradedReason, scan.Findings.Count);
        else
            logger.LogInformation("Nuclei scan completed: {Count} findings in {Duration}s",
                scan.Findings.Count, scan.DurationSeconds);
    }

    internal static List<string> BuildEndpointUrls(
        string swaggerPath, string baseUrl, ILogger logger, out string? degradedReason)
    {
        var urls = new List<string> { baseUrl };
        degradedReason = null;

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
        catch (Exception ex)
        {
            degradedReason = "swagger parse failed; scanned base URL only";
            logger.LogWarning(ex,
                "Nuclei: swagger parse failed for '{Path}' — scanning base URL only (degraded)", swaggerPath);
        }

        return urls.Distinct().ToList();
    }

    internal static string RewriteLocalhostForDocker(string url, string dockerHostname = "host.docker.internal") =>
        url.Replace("://localhost", $"://{dockerHostname}")
           .Replace("://127.0.0.1", $"://{dockerHostname}");

}
