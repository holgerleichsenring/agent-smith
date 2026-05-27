using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads each discovered context's coding-principles.md (p0158f + p0161a).
/// Iterates ContextKeys.Sandboxes keys; per key derives the per-context
/// MetaDir from ContextKeys.SandboxDiscoveries and reads
/// coding-principles.md. Populates ContextKeys.RepoCodingPrinciples (now
/// keyed by sandbox key) and legacy ContextKeys.DomainRules (concatenated
/// with per-key headers; verbatim for single-key).
/// </summary>
public sealed class LoadCodingPrinciplesHandler(
    ISandboxFileReaderFactory readerFactory,
    ISystemEventPublisher systemEvents,
    IRunContextAccessor runContext,
    ILogger<LoadCodingPrinciplesHandler> logger)
    : ICommandHandler<LoadCodingPrinciplesContext>
{
    private const string DefaultRelativePath = ProjectMetaPaths.CodingPrinciples;

    public async Task<CommandResult> ExecuteAsync(
        LoadCodingPrinciplesContext context, CancellationToken cancellationToken)
    {
        if (!SandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Ok("No Sandboxes/SandboxDiscoveries in pipeline context, skipping");

        var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, sandbox) in sandboxes)
        {
            if (!discoveries.TryGetValue(key, out var discovery)) continue;
            var content = await TryReadOneAsync(context, sandbox, key, discovery, cancellationToken);
            if (content is not null)
            {
                loaded[key] = content;
                await EmitConfigReadAsync(
                    path: $"{ProjectMetaPaths.MetaDirFor(discovery.ContextName)}/{ProjectMetaPaths.CodingPrinciplesFile}",
                    sizeBytes: content.Length,
                    cancellationToken);
            }
        }

        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.RepoCodingPrinciples, loaded);
        if (loaded.Count > 0)
            context.Pipeline.Set(ContextKeys.DomainRules, Aggregate(loaded));

        return CommandResult.Ok($"Loaded {loaded.Count} of {sandboxes.Count} context principles");
    }

    // p0173c: emit a system event per coding-principles.md successfully read.
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

    private async Task<string?> TryReadOneAsync(
        LoadCodingPrinciplesContext context, ISandbox sandbox, string key,
        RemoteContextDiscovery discovery, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        var direct = Path.Combine(Repository.SandboxWorkPath, context.RelativePath);
        if (await reader.ExistsAsync(direct, ct))
            return await reader.ReadRequiredAsync(direct, ct);

        if (!string.Equals(context.RelativePath, DefaultRelativePath, StringComparison.OrdinalIgnoreCase))
            return null;

        var nested = $"{ProjectMetaPaths.MetaDirFor(discovery.ContextName)}/{ProjectMetaPaths.CodingPrinciplesFile}";
        if (!await reader.ExistsAsync(nested, ct)) return null;
        var content = await reader.ReadRequiredAsync(nested, ct);
        logger.LogInformation("{Key}: loaded principles from {Path} ({Chars} chars)", key, nested, content.Length);
        return content;
    }

    private static string Aggregate(IReadOnlyDictionary<string, string> perKey)
    {
        if (perKey.Count == 1) return perKey.Values.First();
        var sb = new StringBuilder();
        var first = true;
        foreach (var (key, content) in perKey)
        {
            if (!first) sb.Append("\n\n---\n\n");
            sb.Append($"## {key}\n\n");
            sb.Append(content.TrimEnd());
            first = false;
        }
        return sb.ToString();
    }
}
