using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Spawns a Spectral Docker container to lint the swagger spec.
/// Stores parsed findings in the pipeline context.
/// </summary>
public sealed class SpawnSpectralHandler(
    ISpectralScanner spectralScanner,
    ILogger<SpawnSpectralHandler> logger)
    : ICommandHandler<SpawnSpectralContext>
{
    public async Task<CommandResult> ExecuteAsync(
        SpawnSpectralContext context, CancellationToken cancellationToken)
    {
        context.Pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);

        if (spec is null || string.IsNullOrWhiteSpace(spec.RawJson))
            return CommandResult.Fail("No swagger spec available for Spectral lint");

        var swaggerPath = WriteTempSwagger(spec);

        try
        {
            var result = await spectralScanner.LintAsync(swaggerPath, cancellationToken);
            context.Pipeline.Set(ContextKeys.SpectralResult, result);

            logger.LogInformation(
                "Spectral lint complete: {Total} findings ({Errors} errors, {Warnings} warnings) in {Duration}s",
                result.Findings.Count, result.ErrorCount, result.WarnCount, result.DurationSeconds);

            return CommandResult.Ok(
                $"Spectral: {result.Findings.Count} findings ({result.ErrorCount}E/{result.WarnCount}W) in {result.DurationSeconds}s");
        }
        finally
        {
            if (File.Exists(swaggerPath))
                File.Delete(swaggerPath);
        }
    }

    private static string WriteTempSwagger(SwaggerSpec spec)
    {
        var path = Path.Combine(Path.GetTempPath(), $"swagger-spectral-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, spec.RawJson);
        return path;
    }
}
