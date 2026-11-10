using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Memory;

/// <summary>2026-11-10-fcd7a: one place a run's memories live, by the label a tool and a citation name it with.</summary>
public sealed record MemorySource(string Label, string LedgerKey, IMemoryStore Store)
{
    public bool IsProject => Label == ProjectMemorySource.Label;
}

/// <summary>
/// 2026-11-10-fcd7a: every memory source of a run — the project's store first, under '@project', when the
/// project has a config row, then each of the run's repositories under its own name.
/// </summary>
public sealed class MemorySources(IProjectMemoryRepository projects, MemoryRepositoryKey keys)
{
    public async Task<IReadOnlyList<MemorySource>> OfAsync(
        PipelineContext pipeline, IRunRepositories repositories, string repoRoot, ILogger? logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(repositories);
        var sources = new List<MemorySource>();
        if (await ProjectAsync(pipeline, ct) is { } project) sources.Add(project);
        foreach (var name in repositories.Names())
            if (repositories.FilesOf(name) is { } files)
                sources.Add(new MemorySource(name, keys.Of(pipeline, name), new FileMemoryStore(files, repoRoot, logger)));
        return sources;
    }

    /// <summary>The project's source, or null when the run names no project or the project has no config row.</summary>
    public async Task<MemorySource?> ProjectAsync(PipelineContext pipeline, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<string>(ContextKeys.ProjectName, out var name) || string.IsNullOrWhiteSpace(name)) return null;
        return await projects.ResolveProjectAsync(name, ct) is { } id
            ? new MemorySource(ProjectMemorySource.Label, keys.OfProject(id), new ProjectMemoryStore(projects, id))
            : null;
    }
}
