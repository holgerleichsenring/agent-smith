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
    /// <summary>
    /// The scanner image, compiled in like the other tool-runner images and outside
    /// <c>sandbox.allowed_registries</c> for the reason recorded on
    /// <see cref="ToolRunnerConfig.Images"/>. Named rather than inlined so the test
    /// that pins the compiled-in set can see it (2026-08-25-014d).
    /// </summary>
    public const string ScannerImage = "ghcr.io/zaproxy/zaproxy:stable";

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

        var limitSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : config.ContainerTimeout;

        var toolRequest = new ToolRunRequest(
            ScannerImage, arguments, inputFiles,
            OutputFileName: "zap-report.json",
            ExtraHosts: extraHosts,
            TimeoutSeconds: limitSeconds,
            WorkDir: "/zap/wrk");

        var result = await toolRunner.RunAsync(toolRequest, cancellationToken);

        var output = result.OutputFileContent ?? result.Stdout;
        var findings = ZapReportParser.ParseZapJson(output);

        logger.LogDebug("ZAP exit code: {ExitCode}, stdout: {StdoutLen}chars, output file: {HasOutput}",
            result.ExitCode, result.Stdout.Length, result.OutputFileContent is not null);

        if (!string.IsNullOrWhiteSpace(result.Stderr) && result.ExitCode > 3)
            logger.LogWarning("ZAP stderr: {Stderr}", result.Stderr[..Math.Min(500, result.Stderr.Length)]);

        return Report(result, findings, request.ScanType, limitSeconds);
    }

    /// <summary>
    /// 2026-08-30-26ed: a run the runner cut off at its container limit says so, rather than
    /// reporting its partial finding count as a completed scan. Zero findings after a cut-off
    /// is the dangerous case — rendered as a completion it reads as a clean target.
    /// </summary>
    private ZapResult Report(
        ToolResult result, IReadOnlyList<ZapFinding> findings, string scanType, int limitSeconds)
    {
        var reason = result.CutOff ? ScanDegradation.CutOffAt(limitSeconds) : null;

        if (reason is not null)
            logger.LogWarning(
                "ZAP {ScanType} scan {Reason}: {Count} findings from the part it reached",
                scanType, reason, findings.Count);
        else
            logger.LogInformation(
                "ZAP {ScanType} scan completed: {Count} findings in {Duration}s",
                scanType, findings.Count, result.DurationSeconds);

        return new ZapResult(
            findings, result.DurationSeconds, scanType, result.ExitCode,
            Degraded: reason is not null, DegradedReason: reason);
    }

    internal static string RewriteLocalhostForDocker(string url, string dockerHostname = "host.docker.internal") =>
        url.Replace("://localhost", $"://{dockerHostname}")
           .Replace("://127.0.0.1", $"://{dockerHostname}");
}
