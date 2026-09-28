using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Source resolver for api-scan. An explicit --source-path must exist; otherwise the
/// configured source (a local path or a remote clone via IHostSourceCloner) is used, and
/// any failure there leaves SourcePath unset so the scan continues in passive mode.
/// Operates on the primary repo only (Configs[0]).
/// </summary>
public sealed class TryCheckoutSourceHandler(
    IHostSourceCloner cloner,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    ILogger<TryCheckoutSourceHandler> logger)
    : ICommandHandler<TryCheckoutSourceContext>, IConceptWriter
{
    public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; } =
        [new ConceptDeclaration("source_available", ConceptType.Bool)];

    public async Task<CommandResult> ExecuteAsync(
        TryCheckoutSourceContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var cliOverride = TryHonorCliOverride(pipeline);
        if (cliOverride is not null) return cliOverride;

        var source = context.Source;
        if (source.Type == RepoType.Local)
            return ResolveLocal(source, pipeline);

        return await CloneRemoteAsync(context, cancellationToken);
    }

    private CommandResult? TryHonorCliOverride(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.SourcePath, out var path) || string.IsNullOrWhiteSpace(path))
            return null;
        if (!Directory.Exists(path))
            return CommandResult.Fail($"--source-path '{path}' does not exist");
        PublishLocalRepository(pipeline, path);
        logger.LogInformation("Source: {Path} (CLI override)", path);
        EmitBanner(pipeline, sourcePath: path);
        return Ok();
    }

    private CommandResult ResolveLocal(RepoConnection source, PipelineContext pipeline)
    {
        if (string.IsNullOrWhiteSpace(source.Path) || source.Path == EphemeralSource.NoSourcePath)
            return WarnPassive(pipeline, "No source given — passive mode");
        if (!Directory.Exists(source.Path))
            return WarnPassive(pipeline, $"Configured source path '{source.Path}' does not exist — passive mode");
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
        // Passive mode has no checked-out source, but a synthetic Repository must
        // still be published: downstream builders (LoadContext, LoadCodingPrinciples,
        // AgenticMaster, …) read ContextKeys.Repository unconditionally, and its
        // LocalPath is the fixed sandbox work path regardless of a real clone. Without
        // this, api-scan — which is passive by design (it targets a running API, not a
        // repo) — dies at the first builder with a missing-key error.
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("(passive)"), string.Empty));
        EmitBanner(pipeline, sourcePath: null);
        return Ok();
    }

    private void EmitBanner(PipelineContext pipeline, string? sourcePath)
    {
        var hasSource = sourcePath is not null;
        conceptsFactory(pipeline).SetBool("source_available", hasSource);
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
