using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-11-10-85e2: a design turn's memory index, read remotely by the grounding and composed exactly as a run's is
/// (<see cref="MemoryIndexReader"/>): each repository's index with its memories' standing, holding first, disputed
/// entries left out, labelled per repository when there are several.
/// </summary>
public sealed class DesignMemoryIndex(MemoryStandingReader standings, MemoryIndexComposer composer)
{
    public async Task<string> ComposeAsync(
        PipelineContext pipeline, IReadOnlyList<ContextDocument> indexes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(indexes);
        var composed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var index in indexes.Where(i => !string.IsNullOrWhiteSpace(i.Content)))
            composed[index.SandboxKey] = composer.Compose(index.Content.Trim(), await standings.OfAsync(pipeline, index.SandboxKey, ct));
        return MemoryIndexReader.Aggregate(composed, labelled: composed.Count > 1);
    }
}
