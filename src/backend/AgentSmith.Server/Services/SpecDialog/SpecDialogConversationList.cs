using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The caller's dashboard conversations, open and closed, each titled by what the person said
/// and marked by what it filed. Its own read, off the per-dialog view the page reloads after
/// every message, because this one parses every listed transcript.
/// <para>
/// Filtered by owner in the query — another principal's conversations are never loaded, let
/// alone listed — and capped at the most recently created, so a conversation created long
/// ago and resumed today can fall outside the cap.
/// </para>
/// </summary>
public sealed class SpecDialogConversationList(
    SpecDialogSessionRepository repository, SpecDialogLatestOutcomeStore latestOutcome)
{
    internal const int Cap = 50;
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    public async Task<IReadOnlyList<SpecDialogSessionSummary>> ListAsync(
        string owner, CancellationToken cancellationToken) =>
        [.. (await repository.ListByOwnerAsync(Platform, owner, Cap, cancellationToken))
            .Select(Summary)];

    private SpecDialogSessionSummary Summary(SpecDialogSession session)
    {
        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson);
        return new SpecDialogSessionSummary(
            session.SessionId, session.Project, transcript.Count, session.LastActivityAt,
            SpecDialogConversationTitle.Of(transcript),
            Outcome(latestOutcome.Of(session)),
            session.IsOpen ? session.ThreadId : null);
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
