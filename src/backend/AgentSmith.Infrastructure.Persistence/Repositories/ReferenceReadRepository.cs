using System.Text;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-04-96a0c: the paths earlier turns opened of an uploaded set — rows of kind
/// <see cref="ReferenceFileKind.Reads"/> under the set's id, like the note, so they never join the set's files, name,
/// count or hash, and need no migration. Each row holds sorted, newline-joined paths.
/// <para>
/// The turn gate is per process and several replicas are a requirement, so two writes can race. A read returns the
/// UNION of every row of the set, and a write adds its merged row before it deletes, by id, the rows it merged — so
/// a racing write loses nothing, and two replicas deleting one row raise no concurrency error.
/// </para>
/// </summary>
public sealed class ReferenceReadRepository(IUnitOfWork unitOfWork)
{
    private const string ReadsPath = "READS.txt";
    private const string ReadsMediaType = "text/plain";

    /// <summary>Every set's opened paths of one conversation, by set id.</summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlySet<string>>> ReadsAsync(string sessionId, CancellationToken ct)
    {
        var rows = await unitOfWork.Set<ReferenceFile>().AsNoTracking()
            .Where(f => f.SessionId == sessionId && f.Kind == ReferenceFileKind.Reads)
            .Select(f => new { f.SetId, f.Content }).ToListAsync(ct);
        return rows.GroupBy(r => r.SetId, StringComparer.Ordinal).ToDictionary(
            g => g.Key, g => (IReadOnlySet<string>)g.SelectMany(r => Paths(r.Content)).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    /// <summary>Merges <paramref name="paths"/> into the set's stored paths and returns them; null, storing nothing,
    /// when the conversation no longer holds the set.</summary>
    public async Task<IReadOnlySet<string>?> AddAsync(
        string sessionId, string setId, IEnumerable<string> paths, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var held = await unitOfWork.Set<ReferenceFile>().AnyAsync(
            f => f.SessionId == sessionId && f.SetId == setId && f.Kind == ReferenceFileKind.Site, ct);
        if (!held) return null;
        var rows = await unitOfWork.Set<ReferenceFile>().AsNoTracking()
            .Where(f => f.SessionId == sessionId && f.SetId == setId && f.Kind == ReferenceFileKind.Reads)
            .Select(f => new { f.Id, f.Content }).ToListAsync(ct);
        var merged = rows.SelectMany(r => Paths(r.Content)).Concat(paths).ToHashSet(StringComparer.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(string.Join("\n", merged.Order(StringComparer.Ordinal)));
        unitOfWork.Add(new ReferenceFile
        {
            SessionId = sessionId, SetId = setId, Kind = ReferenceFileKind.Reads, RelativePath = ReadsPath,
            MediaType = ReadsMediaType, Content = bytes, Length = bytes.LongLength,
        });
        await unitOfWork.SaveChangesAsync(ct);
        var mergedIds = rows.Select(r => r.Id).ToList();
        if (mergedIds.Count > 0)
            await unitOfWork.Set<ReferenceFile>().Where(f => mergedIds.Contains(f.Id)).ExecuteDeleteAsync(ct);
        return merged;
    }

    private static IEnumerable<string> Paths(byte[] content) =>
        Encoding.UTF8.GetString(content).Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
