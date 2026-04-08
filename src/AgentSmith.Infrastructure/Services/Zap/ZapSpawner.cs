using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Zap;

/// <summary>
/// Runs an OWASP ZAP scan via IToolRunner.
/// Configuration loaded from config/zap.yaml.
/// </summary>
public sealed class ZapSpawner(
    IToolRunner toolRunner,
    ZapConfig config,
    ILogger<ZapSpawner> logger) : IZapScanner
{
    public async Task<ZapResult> ScanAsync(
        ZapScanRequest request, CancellationToken cancellationToken)
    {
        var dockerTarget = RewriteLocalhostForDocker(request.TargetUrl);
        var isLocal = dockerTarget.Contains("host.docker.internal");

        logger.LogInformation("Starting ZAP {ScanType} scan: {Target} (container: {DockerTarget})",
            request.ScanType, request.TargetUrl, dockerTarget);

        var inputFiles = new Dictionary<string, string>();
        var arguments = BuildArguments(request.ScanType, dockerTarget, request.SwaggerPath, inputFiles);

        var extraHosts = isLocal
            ? new Dictionary<string, string> { ["host.docker.internal"] = "host-gateway" }
            : null;

        var toolRequest = new ToolRunRequest(
            "ghcr.io/zaproxy/zaproxy:stable", arguments, inputFiles,
            OutputFileName: "zap-report.json",
            ExtraHosts: extraHosts,
            TimeoutSeconds: request.TimeoutSeconds > 0 ? request.TimeoutSeconds : config.ContainerTimeout);

        var result = await toolRunner.RunAsync(toolRequest, cancellationToken);

        var output = result.OutputFileContent ?? result.Stdout;
        var findings = ParseZapJson(output);

        logger.LogInformation(
            "ZAP {ScanType} scan completed: {Count} findings in {Duration}s",
            request.ScanType, findings.Count, result.DurationSeconds);

        if (!string.IsNullOrWhiteSpace(result.Stderr) && result.ExitCode != 0)
            logger.LogWarning("ZAP stderr: {Stderr}", result.Stderr[..Math.Min(500, result.Stderr.Length)]);

        return new ZapResult(findings, result.DurationSeconds, request.ScanType);
    }

    internal static List<string> BuildArguments(
        string scanType, string dockerTarget, string? swaggerPath,
        Dictionary<string, string> inputFiles)
    {
        return scanType.ToLowerInvariant() switch
        {
            "full-scan" => BuildFullScanArgs(dockerTarget),
            "api-scan" => BuildApiScanArgs(dockerTarget, swaggerPath, inputFiles),
            _ => BuildBaselineArgs(dockerTarget),
        };
    }

    private static List<string> BuildBaselineArgs(string target)
    {
        return ["zap-baseline.py", "-t", target, "-J", "{work}/zap-report.json", "-l", "WARN", "--auto"];
    }

    private static List<string> BuildFullScanArgs(string target)
    {
        return ["zap-full-scan.py", "-t", target, "-J", "{work}/zap-report.json", "-l", "WARN", "--auto"];
    }

    private static List<string> BuildApiScanArgs(
        string target, string? swaggerPath, Dictionary<string, string> inputFiles)
    {
        if (!string.IsNullOrWhiteSpace(swaggerPath) && File.Exists(swaggerPath))
        {
            inputFiles["swagger.json"] = File.ReadAllText(swaggerPath);
            return ["zap-api-scan.py", "-t", "{work}/swagger.json", "-f", "openapi", "-J", "{work}/zap-report.json", "-l", "WARN", "--auto"];
        }

        // Fallback: use target URL directly for api-scan
        return ["zap-api-scan.py", "-t", target, "-f", "openapi", "-J", "{work}/zap-report.json", "-l", "WARN", "--auto"];
    }

    internal static string RewriteLocalhostForDocker(string url) =>
        url.Replace("://localhost", "://host.docker.internal")
           .Replace("://127.0.0.1", "://host.docker.internal");

    internal static List<ZapFinding> ParseZapJson(string output)
    {
        var findings = new List<ZapFinding>();

        if (string.IsNullOrWhiteSpace(output))
            return findings;

        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            if (!root.TryGetProperty("site", out var sites) || sites.ValueKind != JsonValueKind.Array)
                return findings;

            foreach (var site in sites.EnumerateArray())
            {
                if (!site.TryGetProperty("alerts", out var alerts) || alerts.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var alert in alerts.EnumerateArray())
                {
                    var alertRef = GetString(alert, "alertRef");
                    var name = GetString(alert, "name");
                    var riskDesc = GetString(alert, "riskdesc");
                    var confidence = GetString(alert, "confidence");
                    var desc = GetString(alert, "desc");
                    var solution = GetStringOrNull(alert, "solution");
                    var cweId = GetStringOrNull(alert, "cweid");
                    var wascId = GetStringOrNull(alert, "wascid");

                    // Extract risk level from riskdesc (e.g. "Medium (High)" -> "Medium")
                    var riskLevel = ExtractRiskLevel(riskDesc);

                    // Get first instance URL or empty
                    var url = "";
                    if (alert.TryGetProperty("instances", out var instances) && instances.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var inst in instances.EnumerateArray())
                        {
                            if (inst.TryGetProperty("uri", out var uri))
                            {
                                url = uri.GetString() ?? "";
                                break;
                            }
                        }
                    }

                    var countStr = GetString(alert, "count");
                    _ = int.TryParse(countStr, out var count);

                    findings.Add(new ZapFinding(alertRef, name, riskLevel, confidence, url, desc, solution, cweId, wascId, count));
                }
            }
        }
        catch
        {
            // If JSON parsing fails entirely, return empty findings
        }

        return findings;
    }

    internal static string ExtractRiskLevel(string riskDesc)
    {
        if (string.IsNullOrWhiteSpace(riskDesc))
            return "Informational";

        // ZAP riskdesc format: "Medium (High)" where first part is risk, parenthetical is confidence
        var parenIndex = riskDesc.IndexOf('(');
        var risk = parenIndex > 0 ? riskDesc[..parenIndex].Trim() : riskDesc.Trim();
        return string.IsNullOrEmpty(risk) ? "Informational" : risk;
    }

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) ? value.GetString() ?? "" : "";

    private static string? GetStringOrNull(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) ? value.GetString() : null;
}
