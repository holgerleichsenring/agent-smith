using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-01-283db: a conversation's uploaded websites, each a SET of reference files that
/// arrived together. A set is stored whole in one save — all of it or none of it — and read as
/// a summary; the files' bytes are never part of a listing.
/// </summary>
public sealed class ReferenceSetRepository(IUnitOfWork unitOfWork)
{
    /// <summary>The name a set whose paths share no folder is listed under.</summary>
    public const string UnnamedSet = "site";

    /// <summary>
    /// Stores <paramref name="files"/> as one new site set of the conversation, in one save. Each
    /// file carries its path, media type and bytes; the session, the set and the kind are stamped
    /// here.
    /// </summary>
    public async Task<ReferenceSetSummary> AddAsync(
        string sessionId, IReadOnlyList<ReferenceFile> files, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0) throw new ArgumentException("A set holds at least one file.", nameof(files));
        var setId = Guid.NewGuid().ToString("N");
        foreach (var file in files)
        {
            file.SessionId = sessionId;
            file.SetId = setId;
            file.Kind = ReferenceFileKind.Site;
            file.Length = file.Content.LongLength;
            unitOfWork.Add(file);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Summary(setId, [.. files.Select(f => (f.RelativePath, f.Length, f.CreatedAt))]);
    }

    /// <summary>Every site set of one conversation, oldest first.</summary>
    public async Task<IReadOnlyList<ReferenceSetSummary>> ListAsync(string sessionId, CancellationToken ct)
    {
        var rows = await unitOfWork.Set<ReferenceFile>().AsNoTracking()
            .Where(f => f.SessionId == sessionId && f.Kind == ReferenceFileKind.Site)
            .Select(f => new { f.SetId, f.RelativePath, f.Length, f.CreatedAt })
            .ToListAsync(ct);
        return [.. rows.GroupBy(r => r.SetId)
            .Select(set => Summary(set.Key, [.. set.Select(r => (r.RelativePath, r.Length, r.CreatedAt))]))
            .OrderBy(s => s.At).ThenBy(s => s.SetId, StringComparer.Ordinal)];
    }

    private static ReferenceSetSummary Summary(
        string setId, IReadOnlyList<(string Path, long Length, DateTimeOffset At)> files) =>
        new(setId, NameOf(files.Select(f => f.Path)), files.Count, files.Sum(f => f.Length), files.Min(f => f.At));

    // The folder a folder upload, or an unpacked archive, puts every file under.
    private static string NameOf(IEnumerable<string> paths)
    {
        var tops = paths.Select(p => p.IndexOf('/') is var slash and > 0 ? p[..slash] : null).Distinct().ToList();
        return tops is [{ } only] ? only : UnnamedSet;
    }
}
