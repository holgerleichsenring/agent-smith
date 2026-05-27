using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads each discovered context's context.yaml (p0158f + p0161a). Iterates
/// ContextKeys.Sandboxes keys; per key derives the per-context MetaDir
/// (.agentsmith/contexts/&lt;contextName&gt;) from ContextKeys.SandboxDiscoveries
/// and reads context.yaml. Populates ContextKeys.RepoContextYamls (now keyed
/// by sandbox key) and legacy ContextKeys.ProjectContext (= first sandbox
/// key's YAML).
/// </summary>
public sealed class LoadContextHandler(
    ISandboxFileReaderFactory readerFactory,
    ISystemEventPublisher systemEvents,
    IRunContextAccessor runContext,
    ILogger<LoadContextHandler> logger)
    : ICommandHandler<LoadContextContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadContextContext context, CancellationToken cancellationToken)
    {
        if (!SandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Ok("No Sandboxes/SandboxDiscoveries in pipeline context, skipping");

        var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, sandbox) in sandboxes)
        {
            if (!discoveries.TryGetValue(key, out var discovery)) continue;
            var content = await TryReadOneAsync(sandbox, key, discovery, cancellationToken);
            if (content is not null)
            {
                loaded[key] = content;
                await EmitConfigReadAsync(
                    path: $"{ProjectMetaPaths.MetaDirFor(discovery.ContextName)}/{ProjectMetaPaths.ContextYamlFile}",
                    sizeBytes: content.Length,
                    cancellationToken);
            }
        }

        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.RepoContextYamls, loaded);
        var primaryKey = sandboxes.Keys.First();
        if (loaded.TryGetValue(primaryKey, out var primary))
            context.Pipeline.Set(ContextKeys.ProjectContext, primary);

        if (loaded.Count == 0)
            return CommandResult.Ok("No project context loaded");
        if (loaded.Count == 1)
            return CommandResult.Ok($"Loaded project context ({loaded.Values.First().Length} chars)");
        return CommandResult.Ok($"Loaded {loaded.Count} of {sandboxes.Count} context(s)");
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

    private async Task<string?> TryReadOneAsync(
        ISandbox sandbox, string key, RemoteContextDiscovery discovery, CancellationToken ct)
    {
        var path = $"{ProjectMetaPaths.MetaDirFor(discovery.ContextName)}/{ProjectMetaPaths.ContextYamlFile}";
        var reader = readerFactory.Create(sandbox);
        var content = await reader.TryReadAsync(path, ct);
        if (content is null)
        {
            logger.LogInformation("{Key}: no {Path}, skipping", key, path);
            return null;
        }
        logger.LogInformation("{Key}: loaded {Path} ({Chars} chars)", key, path, content.Length);
        return content;
    }
}
