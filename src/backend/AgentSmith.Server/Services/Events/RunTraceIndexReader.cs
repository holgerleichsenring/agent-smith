using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0423b: what a traced run's conversation LOOKS like — one header per recorded entry, in
/// call order — and one entry's content when the reader asks for it.
/// <para>
/// A run that was not traced has no entries, and that is an absent reader, not a broken
/// one. The p0427 key format is the single authority on how an entry is named, so the view
/// and a replay read the same recording.
/// </para>
/// </summary>
public sealed class RunTraceIndexReader(IServiceScopeFactory scopeFactory)
{
    public async Task<IReadOnlyList<RunTraceEntryHeader>> ListAsync(
        string runId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var artifacts = scope.ServiceProvider.GetRequiredService<RunArtifactRepository>();
        var rows = await artifacts.ListSizesAsync(runId, RecordedTraceKey.Prefix, cancellationToken);
        return [.. rows.Select(Header).OfType<RunTraceEntryHeader>().OrderBy(h => h.Sequence)];
    }

    public async Task<string?> ReadAsync(
        string runId, int sequence, string label, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var artifacts = scope.ServiceProvider.GetRequiredService<RunArtifactRepository>();
        return await artifacts.ReadAsync(
            runId, RecordedTraceKey.Format(sequence, label), cancellationToken);
    }

    // The size is read off the row; the sequence and label are parsed by the key format
    // that wrote them, so a key nobody can parse is skipped rather than guessed at.
    private static RunTraceEntryHeader? Header((string Kind, int Chars) row) =>
        RecordedTraceKey.TryParse(row.Kind, string.Empty, out var entry)
            ? new RunTraceEntryHeader(entry.Sequence, entry.Label, row.Chars)
            : null;
}
