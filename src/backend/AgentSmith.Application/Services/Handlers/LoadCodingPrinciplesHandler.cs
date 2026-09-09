using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads the principles.md of EVERY context in every sandbox (p0158f + p0161a;
/// 2026-09-04-cf3d fans out over the per-sandbox context list). A flat file at the
/// configured path (default `.agentsmith/principles.md`, the pre-contexts layout) speaks for
/// its whole sandbox; only the default path falls through to the per-context files.
/// Publishes the typed list at ContextKeys.RepoCodingPrinciples and the rendered, labelled
/// aggregate at ContextKeys.DomainRules (verbatim for one document).
/// </summary>
public sealed class LoadCodingPrinciplesHandler(
    ISandboxFileReaderFactory readerFactory,
    ContextDocumentReader documentReader,
    ISystemEventPublisher systemEvents,
    IRunContextAccessor runContext,
    SandboxTargets sandboxTargets,
    ILogger<LoadCodingPrinciplesHandler> logger)
    : ICommandHandler<LoadCodingPrinciplesContext>
{
    private const string DefaultRelativePath = ProjectMetaPaths.Principles;

    public async Task<CommandResult> ExecuteAsync(
        LoadCodingPrinciplesContext context, CancellationToken cancellationToken)
    {
        if (!sandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Ok("No Sandboxes/SandboxDiscoveries in pipeline context, skipping");

        var loaded = new List<ContextDocument>();
        foreach (var (key, sandbox) in sandboxes)
        {
            if (!discoveries.TryGetValue(key, out var representative)) continue;
            var documents = await ReadSandboxAsync(context, key, sandbox, representative, cancellationToken);
            foreach (var document in documents)
            {
                logger.LogInformation("{Key}: loaded principles from {Path} ({Chars} chars)", key, document.Path, document.Content.Length);
                await EmitConfigReadAsync(document.Path, document.Content.Length, cancellationToken);
            }
            loaded.AddRange(documents);
        }

        context.Pipeline.Set<IReadOnlyList<ContextDocument>>(ContextKeys.RepoCodingPrinciples, loaded);
        if (loaded.Count > 0)
            context.Pipeline.Set(ContextKeys.DomainRules, loaded.RenderLabelled());

        return CommandResult.Ok($"Loaded {loaded.Count} principles file(s) across {sandboxes.Count} sandbox(es)");
    }

    // p0173c: emit a system event per principles.md successfully read.
    // RunId comes from the active run scope via IRunContextAccessor.
    private async Task EmitConfigReadAsync(string path, int sizeBytes, CancellationToken ct)
    {
        try
        {
            await systemEvents.PublishAsync(new ConfigFileReadEvent(
                Source: "config-loader",
                Path: path,
                Kind: ConfigFileKind.CodingPrinciplesMd,
                SizeBytes: sizeBytes,
                RunId: runContext.CurrentRunId,
                Timestamp: DateTimeOffset.UtcNow), ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to publish ConfigFileReadEvent for {Path}", path);
        }
    }

    private async Task<IReadOnlyList<ContextDocument>> ReadSandboxAsync(
        LoadCodingPrinciplesContext context, string key, ISandbox sandbox,
        RemoteContextDiscovery representative, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        var direct = Path.Combine(Repository.SandboxWorkPath, context.RelativePath);
        if (await reader.ExistsAsync(direct, ct))
            return [new ContextDocument(key, null, null, direct, await reader.ReadRequiredAsync(direct, ct))];

        if (!string.Equals(context.RelativePath, DefaultRelativePath, StringComparison.OrdinalIgnoreCase))
            return [];

        var contexts = SandboxContextList.InOr(context.Pipeline, key, representative);
        return await documentReader.ReadAsync(sandbox, key, contexts, ProjectMetaPaths.PrinciplesFile, ct);
    }
}
