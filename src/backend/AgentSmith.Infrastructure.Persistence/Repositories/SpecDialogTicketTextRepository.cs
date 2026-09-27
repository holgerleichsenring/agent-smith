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
