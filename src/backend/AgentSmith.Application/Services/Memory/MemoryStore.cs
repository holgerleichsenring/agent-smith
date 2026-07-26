using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: the file-backed experiential-memory store — .agentsmith/memory/ in
/// the TARGET repo checkout, accessed through the sandbox file reader/writer
/// like every other project-meta artifact. Git-native: entries are Markdown
/// files, the index is memory/MEMORY.md. Lenient by design — a malformed entry
/// is skipped with a WARN, an absent store reads as empty.
/// </summary>
public sealed class MemoryStore(ISandboxFileReader reader, string repoRoot, ILogger? logger = null)
{
    private string MemoryDir => Path.Combine(repoRoot, ProjectMetaPaths.Memory);
    private string IndexPath => Path.Combine(repoRoot, ProjectMetaPaths.MemoryIndex);

    public Task<string?> ReadIndexAsync(CancellationToken ct) => reader.TryReadAsync(IndexPath, ct);

    public async Task<IReadOnlyList<MemoryEntry>> ListAsync(CancellationToken ct)
    {
        var files = await reader.ListAsync(MemoryDir, maxDepth: 1, ct);
        var entries = new List<MemoryEntry>();
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (!IsEntryFile(fileName)) continue;
            var entry = await TryReadEntryAsync(fileName, ct);
            if (entry is not null) entries.Add(entry);
        }
        return entries;
    }

    public async Task UpsertAsync(MemoryEntry entry, CancellationToken ct)
    {
        var path = Path.Combine(MemoryDir, $"{entry.Name}.md");
        await reader.WriteAsync(path, MemoryEntrySerializer.Serialize(entry), ct);
        var index = await reader.TryReadAsync(IndexPath, ct);
        await reader.WriteAsync(IndexPath, MemoryIndexUpdater.Upsert(index, entry), ct);
    }

    public async Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, CancellationToken ct)
    {
        var entries = await ListAsync(ct);
        return entries.Where(e => MemoryQueryMatcher.Matches(e, query)).ToList();
    }

    private static bool IsEntryFile(string fileName) =>
        fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(fileName, ProjectMetaPaths.MemoryIndexFile, StringComparison.OrdinalIgnoreCase);

    private async Task<MemoryEntry?> TryReadEntryAsync(string fileName, CancellationToken ct)
    {
        try
        {
            var content = await reader.TryReadAsync(Path.Combine(MemoryDir, fileName), ct);
            if (content is null) return null;
            var entry = MemoryEntryParser.TryParse(fileName, content);
            if (entry is null)
                logger?.LogWarning("Memory entry {File} is malformed — skipped, not a failure", fileName);
            return entry;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Memory entry {File} could not be read — skipped", fileName);
            return null;
        }
    }
}
