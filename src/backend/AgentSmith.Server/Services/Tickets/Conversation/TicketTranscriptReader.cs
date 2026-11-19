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
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SpecDialogSessionRepository>().GetByTicketAsync(project.Tracker.Name, key, ct);
    }

    /// <summary>2026-11-17-a0b6d: the session's proposal awaiting a decision, with its id; null when none awaits.</summary>
    public async Task<(string Id, string Json)?> AwaitingAsync(SpecDialogSession session, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SpecDialogSessionRepository>().Proposals.AwaitingAsync(session.SessionId, ct);
    }

    /// <summary>The open proposal and its drafts; null when none awaits.</summary>
    public async Task<TicketConversationProposalView?> ProposalAsync(SpecDialogSession session, CancellationToken ct) =>
        Proposal(session, await AwaitingAsync(session, ct));

    public IEnumerable<TicketConversationEntryView> Turns(SpecDialogSession session) =>
        Read(session, () => SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson), [])
            .Select((turn, i) => new TicketConversationEntryView(
                $"{TicketConversationSources.Conversation}:{session.SessionId}:{i}", null, null,
                TicketConversationSources.Conversation, TicketConversationKinds.Turn,
                turn.Role == TranscriptRole.User ? TicketConversationRoles.Person : TicketConversationRoles.Agent,
                turn.Author, turn.Role == TranscriptRole.Assistant, turn.At, null, null, turn.Text,
                Format: turn.Role == TranscriptRole.User ? TicketTextFormat.Text : TicketTextFormat.Markdown));

    /// <summary>The <paramref name="awaiting"/> proposal of <paramref name="session"/> and its drafts; null when none awaits.</summary>
    public TicketConversationProposalView? Proposal(SpecDialogSession session, (string Id, string Json)? awaiting)
    {
        // 2026-11-10-ccd9: the proposal awaiting a decision, with the id the page's approve sends.
        if (awaiting is not { Id: var id, Json: var json }) return null;
        var ratified = Read(session, () => RatifiedMocks.Of(SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson)),
            new Dictionary<string, int>());
        return Read<TicketConversationProposalView?>(session,
            () => Of(OutcomeProposalJson.Read(json), ratified) with { ProposalId = id }, null);
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
        OfferLed = proposal.OfferLed, FilingOffer = proposal.FilingOffer, // 2026-11-19-1b72c: the offer gate, as the push
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
