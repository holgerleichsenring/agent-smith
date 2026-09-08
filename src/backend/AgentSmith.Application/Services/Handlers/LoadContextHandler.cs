using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads the context.yaml of EVERY context in every sandbox (p0158f + p0161a; 2026-09-04-cf3d
/// fans out over the per-sandbox context list — two contexts sharing a toolchain image share
/// a sandbox, and the master used to see only the first). Publishes the typed list at
/// ContextKeys.RepoContextYamls and the primary sandbox's documents, rendered and labelled,
/// at ContextKeys.ProjectContext.
/// </summary>
public sealed class LoadContextHandler(
    ContextDocumentReader documentReader,
    ISystemEventPublisher systemEvents,
    IRunContextAccessor runContext,
    SandboxTargets sandboxTargets,
    ILogger<LoadContextHandler> logger)
    : ICommandHandler<LoadContextContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadContextContext context, CancellationToken cancellationToken)
    {
        if (!sandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Ok("No Sandboxes/SandboxDiscoveries in pipeline context, skipping");

        var loaded = new List<ContextDocument>();
        foreach (var (key, sandbox) in sandboxes)
        {
            if (!discoveries.TryGetValue(key, out var representative)) continue;
            var contexts = SandboxContextList.InOr(context.Pipeline, key, representative);
            var documents = await documentReader.ReadAsync(
                sandbox, key, contexts, ProjectMetaPaths.ContextYamlFile, cancellationToken);
            logger.LogInformation("{Key}: loaded {Loaded} of {Contexts} context.yaml", key, documents.Count, contexts.Count);
            foreach (var document in documents)
                await EmitConfigReadAsync(document.Path, document.Content.Length, cancellationToken);
            loaded.AddRange(documents);
        }

        context.Pipeline.Set<IReadOnlyList<ContextDocument>>(ContextKeys.RepoContextYamls, loaded);
        var primaryKey = sandboxes.Keys.First();
        var primary = loaded.Where(d => d.SandboxKey == primaryKey).ToList();
        if (primary.Count > 0)
            context.Pipeline.Set(ContextKeys.ProjectContext, primary.RenderLabelled());

        if (loaded.Count == 0)
            return CommandResult.Ok("No project context loaded");
        if (loaded.Count == 1)
            return CommandResult.Ok($"Loaded project context ({loaded[0].Content.Length} chars)");
        return CommandResult.Ok($"Loaded {loaded.Count} context.yaml file(s) across {sandboxes.Count} sandbox(es)");
    }

    // p0173c: emit a system event for each context.yaml successfully read,
    // populating RunId from the active run scope so the dashboard can
    // cross-reference "which configs did this run read".
    private async Task EmitConfigReadAsync(string path, int sizeBytes, CancellationToken ct)
    {
        try
        {
            await systemEvents.PublishAsync(new ConfigFileReadEvent(
                Source: "config-loader",
                Path: path,
                Kind: ConfigFileKind.ContextYaml,
                SizeBytes: sizeBytes,
                RunId: runContext.CurrentRunId,
                Timestamp: DateTimeOffset.UtcNow), ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to publish ConfigFileReadEvent for {Path}", path);
        }
    }
}
