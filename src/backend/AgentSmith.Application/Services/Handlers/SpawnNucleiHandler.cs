using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Spawns a Nuclei Docker container to scan the API target.
/// Stores parsed findings in the pipeline context.
/// </summary>
public sealed class SpawnNucleiHandler(
    INucleiScanner nucleiScanner,
    ILogger<SpawnNucleiHandler> logger)
    : ICommandHandler<SpawnNucleiContext>
{
    public async Task<CommandResult> ExecuteAsync(
        SpawnNucleiContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<string>(ContextKeys.ApiTarget, out var target)
            || string.IsNullOrWhiteSpace(target))
        {
            return CommandResult.Fail("No API target specified (--target)");
        }

        context.Pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);

        logger.LogDebug("Nuclei: target={Target}, hasSwagger={HasSwagger}, endpoints={Endpoints}",
            target, spec?.RawJson is not null, spec?.Endpoints?.Count ?? 0);

        var swaggerPath = WriteTempSwagger(spec);

        try
        {
            var result = await nucleiScanner.ScanAsync(target, swaggerPath, cancellationToken);
            context.Pipeline.Set(ContextKeys.NucleiResult, result);

            var critical = result.Findings.Count(f => f.Severity == "critical");
            var high = result.Findings.Count(f => f.Severity == "high");
            var medium = result.Findings.Count(f => f.Severity == "medium");

            logger.LogInformation(
                "Nuclei scan complete: {Total} findings ({Critical} critical, {High} high, {Medium} medium) in {Duration}s",
                result.Findings.Count, critical, high, medium, result.DurationSeconds);

            return CommandResult.Ok(
                $"Nuclei: {result.Findings.Count} findings ({critical}C/{high}H/{medium}M) in {result.DurationSeconds}s");
        }
        finally
        {
            if (File.Exists(swaggerPath))
                File.Delete(swaggerPath);
        }
    }

    private static string WriteTempSwagger(SwaggerSpec? spec)
    {
        var path = Path.Combine(Path.GetTempPath(), $"swagger-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, spec?.RawJson ?? "{}");
        return path;
    }
}
