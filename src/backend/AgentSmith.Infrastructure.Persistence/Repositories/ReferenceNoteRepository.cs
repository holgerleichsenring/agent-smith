using System.Text;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-02-075dd: the note on an uploaded set — a row of kind <see cref="ReferenceFileKind.Note"/>
/// under the set's id, so it never joins the set's files, name, count or hash, and needs no
/// migration. There is no unique index on set and kind: a write replaces in one save, and a read
/// takes the newest row should two ever stand.
/// </summary>
public sealed class ReferenceNoteRepository(IUnitOfWork unitOfWork)
{
    private const string NotePath = "NOTE.md";
    private const string NoteMediaType = "text/markdown";

    /// <summary>Every set's note of one conversation, by set id.</summary>
    public async Task<IReadOnlyDictionary<string, string>> NotesAsync(string sessionId, CancellationToken ct)
    {
        var rows = await unitOfWork.Set<ReferenceFile>().AsNoTracking()
            .Where(f => f.SessionId == sessionId && f.Kind == ReferenceFileKind.Note)
            .Select(f => new { f.Id, f.SetId, f.Content }).ToListAsync(ct);
        return rows.GroupBy(r => r.SetId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => Encoding.UTF8.GetString(g.MaxBy(r => r.Id)!.Content), StringComparer.Ordinal);
    }

    /// <summary>Replaces the note of <paramref name="setId"/>; false when the conversation holds no such set.</summary>
    public async Task<bool> SetAsync(string sessionId, string setId, string note, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(note);
        var held = await unitOfWork.Set<ReferenceFile>().AnyAsync(
            f => f.SessionId == sessionId && f.SetId == setId && f.Kind == ReferenceFileKind.Site, ct);
        if (!held) return false;
        foreach (var old in await unitOfWork.Set<ReferenceFile>()
                     .Where(f => f.SessionId == sessionId && f.SetId == setId && f.Kind == ReferenceFileKind.Note).ToListAsync(ct))
            unitOfWork.Remove(old);
        var bytes = Encoding.UTF8.GetBytes(note);
        unitOfWork.Add(new ReferenceFile
        {
            SessionId = sessionId, SetId = setId, Kind = ReferenceFileKind.Note, RelativePath = NotePath,
            MediaType = NoteMediaType, Content = bytes, Length = bytes.LongLength,
        });
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
