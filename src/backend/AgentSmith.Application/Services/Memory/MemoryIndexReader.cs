using System.Text;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: reads .agentsmith/memory/MEMORY.md (the experiential-memory INDEX — one line per memory,
/// bodies stay on disk for recall) from each repository. An absent store reads as an empty index.
/// <para>
/// 2026-10-12-f5f7c: one index per REPOSITORY, labelled by its name — the label recall and remember
/// take — not by sandbox key, so a repository with two toolchain sandboxes is listed once and a
/// template never.
/// </para>
/// <para>
/// 2026-10-13-8762m: shared by LoadMemoryIndex and the spec-execution master start, which re-reads the
/// index so a memory an earlier spec of the series remembered is in the next spec's prompt.
/// </para>
/// </summary>
public sealed class MemoryIndexReader(RunRepositoriesFactory repositoriesFactory, ILogger<MemoryIndexReader> logger)
{
    /// <summary>Reads every repository's index and publishes it as <see cref="ContextKeys.MemoryIndex"/>.</summary>
    public async Task<MemoryIndexRead> RefreshAsync(PipelineContext pipeline, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var repositories = repositoriesFactory.For(pipeline);
        var names = repositories.Names();
        var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var index = await TryReadIndexAsync(repositories.FilesOf(name), name, ct);
            if (!string.IsNullOrWhiteSpace(index)) loaded[name] = index.Trim();
        }

        var read = new MemoryIndexRead(Aggregate(loaded, labelled: names.Count > 1), loaded.Count, names.Count);
        pipeline.Set(ContextKeys.MemoryIndex, read.Index);
        return read;
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

/// <summary>2026-10-13-8762m: the index read, how many repositories had one, and how many were read.</summary>
public sealed record MemoryIndexRead(string Index, int Loaded, int Repositories);
