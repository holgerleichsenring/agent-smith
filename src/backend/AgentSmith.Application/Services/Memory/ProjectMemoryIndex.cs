using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-11-10-fcd7b: the project store's part of a memory index — each entry with its standing and where it came
/// from, quoted, so a statement a person made in a conversation reaches a prompt as data with its source, never as
/// an instruction. Empty when the project holds no memory or has no config row.
/// </summary>
public sealed class ProjectMemoryIndex(
    IProjectMemoryRepository repository, MemoryRepositoryKey keys, MemoryStandingReader standings,
    MemoryIndexComposer composer)
{
    public async Task<string?> ComposeAsync(PipelineContext pipeline, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<string>(ContextKeys.ProjectName, out var name) || string.IsNullOrWhiteSpace(name)) return null;
        if (await repository.ResolveProjectAsync(name, ct) is not { } id) return null;
        var records = await repository.ListAsync(id, ct);
        if (records.Count == 0) return null;
        var index = string.Join('\n', records.Select(Line));
        var composed = composer.Compose(index, await standings.OfKeyAsync(keys.OfProject(id), ProjectMemorySource.Label, ct));
        return string.Join('\n', composed.Split('\n').Select(line => line.Length == 0 ? ">" : $"> {line}"));
    }

    private static string Line(ProjectMemoryRecord record)
    {
        var entry = new MemoryEntry(record.Slug, $"{record.Description} ({Source(record)})",
            MemoryEntryTypes.TryParse(record.Type, out var type) ? type : MemoryEntryType.Reference, record.Body);
        return MemoryIndexUpdater.FormatLine(entry);
    }

    private static string Source(ProjectMemoryRecord record)
    {
        var where = record.SourceKind == ProjectMemorySourceKinds.Conversation
            ? $"conversation {record.SourceId}{(record.SourceRef is null ? string.Empty : $", turn {record.SourceRef}")}"
            : $"run {record.SourceId}{(record.SourceRef is null ? string.Empty : $", spec {record.SourceRef}")}";
        return record.Author is null ? where : $"{where}, by {Tools.MemoryRecallToolHost.QuotedAuthor(record.Author)}"; // 205fg
    }
}
