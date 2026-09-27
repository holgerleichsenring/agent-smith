using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The caller's dashboard conversations, open and closed, each named by what it is ABOUT where a
/// subject has been minted and by what the person said where none has, and marked by what it
/// filed. Its own read, separate from the per-dialog view, because this one
/// parses every listed transcript and reads two further JSON documents per row.
/// <para>
/// 2026-09-17-042em: the page now issues that read on a framework message too, so this is no
/// longer off the message path — it is the more expensive of two reads the same reply can
/// trigger. The page therefore ASKS FIRST: it reads this only while the conversation open in it
/// is not yet listed, is listed with NEITHER a title nor a subject, or is listed with fewer turns
/// than the page has already read. A row nobody can recognise is what the read exists to fix, and
/// it stops once it is fixed — which 2026-09-21-f237a made true again for a conversation opened
/// with a pasted block, whose title is null for good but whose subject is not.
/// </para>
/// <para>
/// Filtered by owner in the query — another principal's conversations are never loaded, let
/// alone listed — and capped at the most recently created, so a conversation created long
/// ago and resumed today can fall outside the cap.
/// </para>
/// </summary>
public sealed class SpecDialogConversationList(
    SpecDialogSessionRepository repository, SpecDialogLatestOutcomeStore latestOutcome,
    SpecDialogTicketTextRepository ticketText)
{
    /// <summary>What a caller that names no limit reads — the panel beside the conversation.</summary>
    internal const int Cap = 50;

    /// <summary>
    /// 2026-09-21-f237b: the most a caller may ask for. The read parses every listed transcript
    /// and reads two further JSON documents per row, so an unbounded "all" would grow one page
    /// load with the operator's whole history and get slower every week with nothing on screen to
    /// explain it. The same ceiling RunListComposer uses, for the same reason.
    /// </summary>
    internal const int MaxPageLimit = 200;

    private const string Platform = DispatcherDefaults.PlatformDashboard;

    /// <summary>
    /// A caller's limit, clamped rather than refused — which is what the two other limit-taking
    /// routes in this tree do. Exposed so a test can prove the clamp without opening 201 sessions.
    /// </summary>
    internal static int ClampLimit(int? limit) => Math.Clamp(limit ?? Cap, 1, MaxPageLimit);

    public async Task<SpecDialogConversationPage> ListAsync(
        string owner, int? limit, CancellationToken cancellationToken)
    {
        var rows = await repository.ListByOwnerAsync(
            Platform, owner, ClampLimit(limit), cancellationToken);
        var total = await repository.CountByOwnerAsync(Platform, owner, cancellationToken);
        // 2026-09-27-5c1eb: one read for the whole page, so a bound row can name its ticket the way
        // the tracker does without turning this list into one query per row.
        var tickets = await ticketText.TicketIdsForAsync(
            [.. rows.Select(r => r.SessionId)], cancellationToken);
        return new SpecDialogConversationPage([.. rows.Select(r => Summary(r, tickets))], total);
    }

    private SpecDialogSessionSummary Summary(
        SpecDialogSession session, IReadOnlyDictionary<string, string> tickets)
    {
        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson);
        return new SpecDialogSessionSummary(
            session.SessionId, session.Project, transcript.Count, session.LastActivityAt,
            SpecDialogConversationTitle.Of(transcript),
            session.Subject,
            Outcome(latestOutcome.Of(session)),
            session.IsOpen ? session.ThreadId : null,
            // A conversation bound to no ticket has no row here, and a bound one whose text read
            // never landed has none either — then the row simply carries no ticket.
            tickets.TryGetValue(session.SessionId, out var ticket) ? ticket : null);
    }

    /// <summary>
    /// Only a filing produces an outcome, so an answer turn after one changes nothing and a
    /// filing that stopped before creating anything shows none. Partial is a filing that
    /// stopped after creating some.
    /// </summary>
    private static SpecDialogConversationOutcome? Outcome(SpecDialogLatestOutcome latest) =>
        latest.Filing is { Filed.Count: > 0 } filing
            ? new(filing.Kind ?? SpecDialogProposalComposer.KindOf(latest.Proposal),
                filing.Filed.Count, filing.Error is not null)
            : null;

}
