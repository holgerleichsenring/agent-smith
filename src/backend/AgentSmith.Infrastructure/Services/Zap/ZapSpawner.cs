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
    ToolRunnerConfig toolRunnerConfig,
    ILogger<ZapSpawner> logger) : IZapScanner
{
    public async Task<ZapResult> ScanAsync(
        ZapScanRequest request, CancellationToken cancellationToken)
    {
        var dockerHostname = toolRunnerConfig.DockerHostname;
        var dockerTarget = RewriteLocalhostForDocker(request.TargetUrl, dockerHostname);
        var isLocal = dockerTarget.Contains(dockerHostname);

        logger.LogInformation("Starting ZAP {ScanType} scan: {Target} (container: {DockerTarget})",
            request.ScanType, request.TargetUrl, dockerTarget);

        var inputFiles = new Dictionary<string, string>();
        var arguments = ZapArgumentBuilder.BuildArguments(request.ScanType, dockerTarget, request.SwaggerPath, inputFiles);

        logger.LogDebug("ZAP container args: {Args}", string.Join(" ", arguments));
        logger.LogDebug("ZAP input files: {Files}, timeout: {Timeout}s, workDir: /zap/wrk",
            inputFiles.Count, request.TimeoutSeconds > 0 ? request.TimeoutSeconds : config.ContainerTimeout);

        var extraHosts = isLocal
            ? new Dictionary<string, string> { [dockerHostname] = "host-gateway" }
            : null;

        var toolRequest = new ToolRunRequest(
            "ghcr.io/zaproxy/zaproxy:stable", arguments, inputFiles,
            OutputFileName: "zap-report.json",
            ExtraHosts: extraHosts,
            TimeoutSeconds: request.TimeoutSeconds > 0 ? request.TimeoutSeconds : config.ContainerTimeout,
            WorkDir: "/zap/wrk");

        var result = await toolRunner.RunAsync(toolRequest, cancellationToken);

        var output = result.OutputFileContent ?? result.Stdout;
        var findings = ZapReportParser.ParseZapJson(output);

        logger.LogInformation(
            "ZAP {ScanType} scan completed: {Count} findings in {Duration}s",
            request.ScanType, findings.Count, result.DurationSeconds);

        logger.LogDebug("ZAP exit code: {ExitCode}, stdout: {StdoutLen}chars, output file: {HasOutput}",
            result.ExitCode, result.Stdout.Length, result.OutputFileContent is not null);

        if (!string.IsNullOrWhiteSpace(result.Stderr) && result.ExitCode > 3)
            logger.LogWarning("ZAP stderr: {Stderr}", result.Stderr[..Math.Min(500, result.Stderr.Length)]);

        return new ZapResult(findings, result.DurationSeconds, request.ScanType, result.ExitCode);
    }

    internal static string RewriteLocalhostForDocker(string url, string dockerHostname = "host.docker.internal") =>
        url.Replace("://localhost", $"://{dockerHostname}")
           .Replace("://127.0.0.1", $"://{dockerHostname}");
}
