using System.Text;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-12-cd4ck: a conversation's working notes — rows of kind <see cref="ReferenceFileKind.Note"/> keyed on the
/// session id, so they live where the conversation's uploads live, every replica reads the same file and the
/// conversation's delete sweeps them. The file is the row with an empty set id. Rows under a set id are the
/// per-upload notes written before (2026-10-02-075dd); a read joins them under their set until the first write
/// replaces them all, so nothing a model recorded is lost.
/// </summary>
public sealed class ConversationNotesRepository(IUnitOfWork unitOfWork)
{
    private const string NotesPath = "NOTES.md";
    private const string NotesMediaType = "text/markdown";

    /// <summary>The conversation's notes, or null when none were written.</summary>
    public async Task<string?> ReadAsync(string sessionId, CancellationToken ct)
    {
        var rows = await Rows(sessionId).AsNoTracking().OrderBy(f => f.Id)
            .Select(f => new { f.SetId, f.Content }).ToListAsync(ct);
        if (rows.Count == 0) return null;
        return string.Join("\n\n", rows.Select(r => r.SetId.Length == 0
            ? Encoding.UTF8.GetString(r.Content)
            : $"From the note on upload set {r.SetId}:\n{Encoding.UTF8.GetString(r.Content)}"));
    }

    /// <summary>Replaces the conversation's notes whole, legacy per-upload rows included.</summary>
    public async Task WriteAsync(string sessionId, string notes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notes);
        foreach (var old in await Rows(sessionId).ToListAsync(ct)) unitOfWork.Remove(old);
        var bytes = Encoding.UTF8.GetBytes(notes);
        unitOfWork.Add(new ReferenceFile
        {
            SessionId = sessionId, SetId = string.Empty, Kind = ReferenceFileKind.Note, RelativePath = NotesPath,
            MediaType = NotesMediaType, Content = bytes, Length = bytes.LongLength,
        });
        await unitOfWork.SaveChangesAsync(ct);
    }

    private IQueryable<ReferenceFile> Rows(string sessionId) =>
        unitOfWork.Set<ReferenceFile>().Where(f => f.SessionId == sessionId && f.Kind == ReferenceFileKind.Note);
}
