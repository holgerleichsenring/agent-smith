using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0380: loads .agentsmith/memory/MEMORY.md (the experiential-memory INDEX —
/// one line per memory, bodies stay on disk for recall) from each sandbox at
/// plan time, mirroring LoadCodingPrinciples. An absent store publishes an
/// empty index — the cheap pointer layer, never an error.
/// </summary>
public sealed class LoadMemoryIndexHandler(
    ISandboxFileReaderFactory readerFactory,
    ILogger<LoadMemoryIndexHandler> logger)
    : ICommandHandler<LoadMemoryIndexContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadMemoryIndexContext context, CancellationToken cancellationToken)
    {
        if (!SandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out _))
        {
            context.Pipeline.Set(ContextKeys.MemoryIndex, string.Empty);
            return CommandResult.Ok("No Sandboxes in pipeline context, skipping");
        }

        var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, sandbox) in sandboxes)
        {
            var index = await TryReadIndexAsync(sandbox, key, cancellationToken);
            if (!string.IsNullOrWhiteSpace(index)) loaded[key] = index.Trim();
        }

        context.Pipeline.Set(ContextKeys.MemoryIndex, Aggregate(loaded));
        return CommandResult.Ok(loaded.Count > 0
            ? $"Loaded memory index from {loaded.Count} of {sandboxes.Count} sandbox(es)"
            : "No memory store present — empty index");
    }

    private async Task<string?> TryReadIndexAsync(ISandbox sandbox, string key, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        var path = Path.Combine(Repository.SandboxWorkPath, ProjectMetaPaths.MemoryIndex);
        var content = await reader.TryReadAsync(path, ct);
        if (content is not null)
            logger.LogInformation("{Key}: loaded memory index ({Chars} chars)", key, content.Length);
        return content;
    }

    private static string Aggregate(IReadOnlyDictionary<string, string> perKey)
    {
        if (perKey.Count == 0) return string.Empty;
        if (perKey.Count == 1) return perKey.Values.First();
        var sb = new StringBuilder();
        foreach (var (key, content) in perKey)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append($"## {key}\n\n{content}");
        }
        return sb.ToString();
    }
}
