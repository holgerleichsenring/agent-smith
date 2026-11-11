using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets.Conversation;

/// <summary>
/// 2026-10-13-9a59h: the ticket's bound Work it out conversation, found by (tracker name, TicketKey) without
/// asking the tracker. A bound conversation is shared by everyone who reaches the dialog surface, so the session's
/// owner is not a turn's author: a person's turn shows the name it recorded, and one that recorded none its role.
/// </summary>
public sealed class TicketTranscriptReader(IServiceScopeFactory scopes, ILogger<TicketTranscriptReader> logger)
{
    public async Task<SpecDialogSession?> SessionAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var session = await ReadAsync(project.Tracker.Name, key, ct);
        // 2026-11-10-ccd9 review: a row from before the deploy is given its id here too, so its Approve can name it;
        // re-read in a scope of its own, because the backfill's set-based write leaves a tracked row as it was.
        if (session is not { IsOpen: true, ProposalId: null, LatestProposalJson: not null }) return session;
        using (var scope = scopes.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SpecDialogProposalClaim>()
                .BackfillAsync(session.Platform, session.ThreadId, SpecDialogProposalState.UnderReview, ct);
        return await ReadAsync(project.Tracker.Name, key, ct);
    }

    private async Task<SpecDialogSession?> ReadAsync(string tracker, string key, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SpecDialogSessionRepository>().GetByTicketAsync(tracker, key, ct);
    }

    public IEnumerable<TicketConversationEntryView> Turns(SpecDialogSession session) =>
        Read(session, () => SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson), [])
            .Select((turn, i) => new TicketConversationEntryView(
                $"{TicketConversationSources.Conversation}:{session.SessionId}:{i}", null, null,
                TicketConversationSources.Conversation, TicketConversationKinds.Turn,
                turn.Role == TranscriptRole.User ? TicketConversationRoles.Person : TicketConversationRoles.Agent,
                turn.Author, turn.Role == TranscriptRole.Assistant, turn.At, null, null, turn.Text,
                Format: turn.Role == TranscriptRole.User ? TicketTextFormat.Text : TicketTextFormat.Markdown));

    /// <summary>The open proposal and its drafts; null when none is open.</summary>
    public TicketConversationProposalView? Proposal(SpecDialogSession session)
    {
        // 2026-11-10-ccd9: the proposal awaiting a decision, with the id the page's approve sends.
        if (SpecDialogAwaitingRow.Json(session) is not { } json) return null;
        var ratified = Read(session, () => RatifiedMocks.Of(SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson)),
            new Dictionary<string, int>());
        return Read<TicketConversationProposalView?>(session,
            () => Of(OutcomeProposalJson.Read(json), ratified) with { ProposalId = SpecDialogAwaitingRow.Id(session) }, null);
    }

    private static TicketConversationProposalView Of(OutcomeProposal proposal, IReadOnlyDictionary<string, int> ratified) => (proposal switch
    {
        SpecOutcome spec => new TicketConversationProposalView("spec", [Draft(spec.Draft, ratified)]),
        EpicOutcome epic => new("epic", [Draft(epic.Parent, ratified), .. epic.Children.Select(c => Draft(c, ratified))]),
        TicketGraphOutcome => new("tickets", []),
        TicketCommentOutcome comment => new("comment", []) { Comment = comment.Comment }, // 2026-10-28-7a69c
        _ => new("bug", []),
    }) with
    {
        Findings = proposal.Findings, ReviewRounds = proposal.ReviewRounds, NotReviewed = proposal.NotReviewed,
        Questions = SpecDialogProposalComposer.KnownIds(proposal), Spend = proposal.Spend, // d854b; the page's own id filter
    };

    private static TicketConversationDraftView Draft(SpecDraft draft, IReadOnlyDictionary<string, int> ratified) =>
        new(draft.SpecId, draft.Goal, draft.Repos, draft.Done, ratified.TryGetValue(draft.SpecId, out var v) ? v : null, draft.Yaml);

    private T Read<T>(SpecDialogSession session, Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or NotSupportedException)
        {
            logger.LogWarning(ex, "The conversation {SessionId} bound to a ticket is unreadable — shown as absent", session.SessionId);
            return fallback;
        }
    }
}
