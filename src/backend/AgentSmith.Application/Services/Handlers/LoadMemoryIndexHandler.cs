using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0380: loads .agentsmith/memory/MEMORY.md (the experiential-memory INDEX —
/// one line per memory, bodies stay on disk for recall) from each repository at
/// plan time, mirroring LoadCodingPrinciples. An absent store publishes an
/// empty index — the cheap pointer layer, never an error.
/// <para>
/// 2026-10-12-f5f7c: one index per REPOSITORY, labelled by its name — the label recall and
/// remember take — not by sandbox key, so a repository with two toolchain sandboxes is listed
/// once and a template never.
/// </para>
/// </summary>
public sealed class LoadMemoryIndexHandler(
    RunRepositoriesFactory repositoriesFactory,
    ILogger<LoadMemoryIndexHandler> logger)
    : ICommandHandler<LoadMemoryIndexContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadMemoryIndexContext context, CancellationToken cancellationToken)
    {
        var repositories = repositoriesFactory.For(context.Pipeline);
        var names = repositories.Names();
        if (names.Count == 0)
        {
            context.Pipeline.Set(ContextKeys.MemoryIndex, string.Empty);
            return CommandResult.Ok("No Sandboxes in pipeline context, skipping");
        }

        var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var index = await TryReadIndexAsync(repositories.FilesOf(name), name, cancellationToken);
            if (!string.IsNullOrWhiteSpace(index)) loaded[name] = index.Trim();
        }

        context.Pipeline.Set(ContextKeys.MemoryIndex, Aggregate(loaded, labelled: names.Count > 1));
        return CommandResult.Ok(loaded.Count > 0
            ? $"Loaded memory index from {loaded.Count} of {names.Count} repositor(ies)"
            : "No memory store present — empty index");
    }

    private async Task<string?> TryReadIndexAsync(ISandboxFileReader? reader, string name, CancellationToken ct)
    {
        if (reader is null) return null;
        var path = Path.Combine(Repository.SandboxWorkPath, ProjectMetaPaths.MemoryIndex);
        var content = await reader.TryReadAsync(path, ct);
        if (content is not null)
            logger.LogInformation("{Repo}: loaded memory index ({Chars} chars)", name, content.Length);
        return content;
    }

    // With more than one repository every index carries its repository's name, even when it is
    // the only one present — the name is what remember and recall are told to pass.
    private static string Aggregate(IReadOnlyDictionary<string, string> perRepository, bool labelled)
    {
        if (perRepository.Count == 0) return string.Empty;
        if (!labelled) return perRepository.Values.First();
        var sb = new StringBuilder();
        foreach (var (name, content) in perRepository)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append($"## {name}\n\n{content}");
        }
        return sb.ToString();
    }
}
