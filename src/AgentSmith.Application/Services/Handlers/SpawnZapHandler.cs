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
            var result = await zapScanner.ScanAsync(request, cancellationToken);
            context.Pipeline.Set(ContextKeys.ZapResult, result);

            if (result.ExitCode != 0)
            {
                context.Pipeline.Set(ContextKeys.ZapFailed, true);
                logger.LogWarning(
                    "ZAP {ScanType} scan failed with exit code {ExitCode} in {Duration}s — DAST skills will be skipped",
                    scanType, result.ExitCode, result.DurationSeconds);

                return CommandResult.Ok(
                    $"ZAP: failed (exit code {result.ExitCode}) in {result.DurationSeconds}s — DAST skills will be skipped");
            }

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
