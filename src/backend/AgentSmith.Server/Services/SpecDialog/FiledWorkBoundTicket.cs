using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-25-c4a6: the ticket a conversation BELONGS to (2026-09-25-8e51b), in the spelling its
/// runs are recorded under.
/// <para>
/// Read off the SESSION ROW and never off a request, for the reason the filing read states: the
/// reads on this surface are addressed by dialog id precisely so that nobody can follow a ticket
/// by naming it.
/// </para>
/// <para>
/// THE TRACKER-NATIVE ID COMES FROM THE TICKET TEXT the binding read (2026-09-25-8e51c). The
/// session row keeps the SPEC KEY spelling — lowercased, every non-alphanumeric character
/// collapsed — and a run's TicketId is the tracker's own; the key cannot be turned back into one.
/// So a conversation whose ticket could not be read is bound to a ticket whose runs nothing can
/// find, and this answers null rather than guessing at an id that would match another ticket's.
/// </para>
/// </summary>
public sealed class FiledWorkBoundTicket(
    SpecDialogSessionRepository sessions, SpecDialogTicketTextRepository ticketText)
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    /// <summary>
    /// The bound ticket, or null for a dialog id with no open session, for a conversation that
    /// belongs to no ticket, and for one whose ticket text was never read.
    /// </summary>
    public async Task<BoundWorkTicket?> OfAsync(string dialogId, CancellationToken ct)
    {
        var session = await sessions.GetOpenByThreadAsync(Platform, dialogId, ct);
        if (session?.Tracker is not { } tracker || session.TicketKey is null) return null;
        var held = await ticketText.GetAsync(session.SessionId, ct);
        return held is null
            ? null
            : new BoundWorkTicket(held.TicketId, held.Title, session.Project, tracker);
    }
}

/// <summary>
/// The ticket a conversation belongs to: what the tracker calls it, what it is called, the
/// conversation's own project, and the tracker CONNECTION its work may be found on.
/// </summary>
public sealed record BoundWorkTicket(
    string TicketId, string Title, string Project, string Tracker);
