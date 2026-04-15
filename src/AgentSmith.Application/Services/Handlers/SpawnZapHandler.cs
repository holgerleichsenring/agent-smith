using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Spawns an OWASP ZAP Docker container to scan the API target.
/// Stores parsed findings in the pipeline context.
/// </summary>
public sealed class SpawnZapHandler(
    IZapScanner zapScanner,
    ILogger<SpawnZapHandler> logger)
    : ICommandHandler<SpawnZapContext>
{
    public async Task<CommandResult> ExecuteAsync(
        SpawnZapContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<string>(ContextKeys.ApiTarget, out var target)
            || string.IsNullOrWhiteSpace(target))
        {
            return CommandResult.Fail("No API target specified (--target)");
        }

        context.Pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);

        var swaggerPath = WriteTempSwagger(spec);

        try
        {
            var scanType = "baseline";
            if (spec is not null && !string.IsNullOrWhiteSpace(spec.RawJson) && spec.RawJson != "{}")
                scanType = "api-scan";

            var request = new ZapScanRequest(target, scanType, swaggerPath, AuthToken: null);
            logger.LogDebug("ZAP request: type={ScanType}, target={Target}, swagger={HasSwagger}",
                scanType, target, swaggerPath is not null);

            var result = await zapScanner.ScanAsync(request, cancellationToken);
            context.Pipeline.Set(ContextKeys.ZapResult, result);

            // ZAP exit codes: 0=pass, 1=info, 2=warnings, 3=failures — all valid scan results
            // Only codes > 3 indicate actual tool errors (crash, config failure, etc.)
            if (result.ExitCode > 3)
            {
                context.Pipeline.Set(ContextKeys.ZapFailed, true);
                logger.LogWarning(
                    "ZAP {ScanType} scan crashed with exit code {ExitCode} in {Duration}s — DAST skills will be skipped",
                    scanType, result.ExitCode, result.DurationSeconds);

                return CommandResult.Ok(
                    $"ZAP: crashed (exit code {result.ExitCode}) in {result.DurationSeconds}s — DAST skills will be skipped");
            }

            logger.LogDebug("ZAP exit code {ExitCode}: {Meaning}",
                result.ExitCode, result.ExitCode switch
                {
                    0 => "pass (no issues)",
                    1 => "pass with informational alerts",
                    2 => "warnings found",
                    3 => "failures found",
                    _ => "unknown"
                });

            var high = result.Findings.Count(f => f.RiskDescription.Equals("High", StringComparison.OrdinalIgnoreCase));
            var medium = result.Findings.Count(f => f.RiskDescription.Equals("Medium", StringComparison.OrdinalIgnoreCase));
            var low = result.Findings.Count(f => f.RiskDescription.Equals("Low", StringComparison.OrdinalIgnoreCase));

            logger.LogInformation(
                "ZAP {ScanType} scan complete: {Total} findings ({High} high, {Medium} medium, {Low} low) in {Duration}s",
                scanType, result.Findings.Count, high, medium, low, result.DurationSeconds);

            return CommandResult.Ok(
                $"ZAP: {result.Findings.Count} findings ({high}H/{medium}M/{low}L) in {result.DurationSeconds}s");
        }
        finally
        {
            if (File.Exists(swaggerPath))
                File.Delete(swaggerPath);
        }
    }

    private static string WriteTempSwagger(SwaggerSpec? spec)
    {
        var path = Path.Combine(Path.GetTempPath(), $"swagger-zap-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, spec?.RawJson ?? "{}");
        return path;
    }
}
