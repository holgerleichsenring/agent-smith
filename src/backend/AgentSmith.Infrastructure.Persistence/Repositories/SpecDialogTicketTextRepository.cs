using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-09-25-8e51c: what a design conversation read from its ticket, one row per conversation.
/// A re-read REPLACES in place: the conversation has one answer to what the ticket says, and a
/// second row would be a second one.
/// </summary>
public sealed class SpecDialogTicketTextRepository(IUnitOfWork unitOfWork)
{
    public Task<SpecDialogTicketText?> GetAsync(string sessionId, CancellationToken ct) =>
        unitOfWork.Set<SpecDialogTicketText>()
            .FirstOrDefaultAsync(t => t.SessionId == sessionId, ct);

    /// <summary>
    /// 2026-09-27-5c1eb: the tracker-native ticket id of each of these conversations that has one,
    /// in ONE query. The conversation list holds up to two hundred rows and is already the expensive
    /// read on this surface, so a per-session read would be two hundred more; and the session row's
    /// own ticket key is the SPEC-KEY spelling — lowercased with every non-alphanumeric collapsed —
    /// so a row marked with it would read "dpg1239" rather than "DPG-1239".
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> TicketIdsForAsync(
        IReadOnlyCollection<string> sessionIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        if (sessionIds.Count == 0) return new Dictionary<string, string>();
        var rows = await unitOfWork.Set<SpecDialogTicketText>()
            .Where(t => sessionIds.Contains(t.SessionId))
            .Select(t => new { t.SessionId, t.TicketId })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.SessionId, r => r.TicketId, StringComparer.Ordinal);
    }

    public async Task SaveAsync(SpecDialogTicketText text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(text);
        var held = await GetAsync(text.SessionId, ct);
        if (held is null) unitOfWork.Add(text);
        else
        {
            held.Title = text.Title;
            held.Text = text.Text;
            held.Truncated = text.Truncated;
            held.Fingerprint = text.Fingerprint;
            held.ReadAt = text.ReadAt;
        }
        await unitOfWork.SaveChangesAsync(ct);
    }
}
