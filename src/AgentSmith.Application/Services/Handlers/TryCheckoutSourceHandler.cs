using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Fail-soft source resolver for api-scan. Honors the --source-path CLI
/// override, then resolves the configured source: block (Local path or
/// remote clone via IHostSourceCloner). The clone lands on the host filesystem
/// because api-scan analyzers (RouteMapper / extractors) read host paths via
/// System.IO. Any failure leaves SourcePath unset and lets the pipeline
/// continue in passive schema-only mode.
/// </summary>
public sealed class TryCheckoutSourceHandler(
    IHostSourceCloner cloner,
    ILogger<TryCheckoutSourceHandler> logger)
    : ICommandHandler<TryCheckoutSourceContext>
{
    public async Task<CommandResult> ExecuteAsync(
        TryCheckoutSourceContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (TryHonorCliOverride(pipeline)) return Ok();

        var source = context.Source;
        if (string.IsNullOrWhiteSpace(source.Type))
        {
            logger.LogDebug("No source configured, passive mode");
            EmitBanner(pipeline, sourcePath: null);
            return Ok();
        }

        if (string.Equals(source.Type, "local", StringComparison.OrdinalIgnoreCase))
            return ResolveLocal(source, pipeline);

        return await CloneRemoteAsync(context, cancellationToken);
    }

    private bool TryHonorCliOverride(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.SourcePath, out var path)) return false;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;
        PublishLocalRepository(pipeline, path);
        logger.LogInformation("Source: {Path} (CLI override)", path);
        EmitBanner(pipeline, sourcePath: path);
        return true;
    }

    private CommandResult ResolveLocal(SourceConfig source, PipelineContext pipeline)
    {
        if (string.IsNullOrWhiteSpace(source.Path) || !Directory.Exists(source.Path))
            return WarnPassive(pipeline, $"Local source path missing or absent: {source.Path}");
        var absolute = Path.GetFullPath(source.Path);
        pipeline.Set(ContextKeys.SourcePath, absolute);
        PublishLocalRepository(pipeline, absolute);
        logger.LogInformation("Source: {Path} (local)", absolute);
        EmitBanner(pipeline, sourcePath: absolute);
        return Ok();
    }

    private static void PublishLocalRepository(PipelineContext pipeline, string localPath) =>
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("(local)"), localPath));

    private async Task<CommandResult> CloneRemoteAsync(
        TryCheckoutSourceContext context, CancellationToken cancellationToken)
    {
        var source = context.Source;
        var pipeline = context.Pipeline;
        if (string.IsNullOrWhiteSpace(source.Url))
            return WarnPassive(pipeline, $"Remote source declared but url missing: {source.Type}");
        var hostPath = await cloner.TryCloneAsync(source, cancellationToken);
        if (hostPath is null)
            return WarnPassive(pipeline, $"git clone failed for {source.Type} {source.Url}");
        pipeline.Set(ContextKeys.SourcePath, hostPath);
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("(remote)"), source.Url!));
        logger.LogInformation("Source: {Path} (cloned from {Type})", hostPath, source.Type);
        EmitBanner(pipeline, sourcePath: hostPath);
        return Ok();
    }

    private CommandResult WarnPassive(PipelineContext pipeline, string message)
    {
        logger.LogWarning("{Message}", message);
        EmitBanner(pipeline, sourcePath: null);
        return Ok();
    }

    private void EmitBanner(PipelineContext pipeline, string? sourcePath)
    {
        var hasSource = sourcePath is not null;
        var active = HasActivePersonas(pipeline);
        var skillCount = EstimateSkillCount(active, hasSource);
        var sourceText = hasSource ? $"Source: {sourcePath}" : "Source: unavailable — passive mode";
        logger.LogInformation("{Source} | ~{Count} skill(s)", sourceText, skillCount);
    }

    private static bool HasActivePersonas(PipelineContext pipeline) =>
        pipeline.TryGet<Dictionary<string, PersonaCredentials>>(ContextKeys.Personas, out var p)
            && p is { Count: > 0 };

    private static int EstimateSkillCount(bool active, bool source) =>
        (active, source) switch
        {
            (false, false) => 4,
            (false, true)  => 7,
            (true,  false) => 8,
            (true,  true)  => 11,
        };

    private static CommandResult Ok() => CommandResult.Ok("source resolved");
}
