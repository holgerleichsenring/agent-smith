using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets.Conversation;

/// <summary>
/// 2026-10-13-9a59h: the ticket's bound Work it out conversation, found by (tracker name, TicketKey) without
/// asking the tracker. A turn names its role, never an author: a bound conversation is shared by everyone who
/// reaches the dialog surface, so the session's owner is not a turn's author.
/// </summary>
public sealed class TicketTranscriptReader(IServiceScopeFactory scopes, ILogger<TicketTranscriptReader> logger)
{
    public async Task<SpecDialogSession?> SessionAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        return await scope.ServiceProvider.GetRequiredService<SpecDialogSessionRepository>()
            .GetByTicketAsync(project.Tracker.Name, key, ct);
    }

    public IEnumerable<TicketConversationEntryView> Turns(SpecDialogSession session) =>
        Read(session, () => SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson), [])
            .Select((turn, i) => new TicketConversationEntryView(
                $"{TicketConversationSources.Conversation}:{session.SessionId}:{i}", null, null,
                TicketConversationSources.Conversation, TicketConversationKinds.Turn,
                turn.Role == TranscriptRole.User ? TicketConversationRoles.Person : TicketConversationRoles.Agent,
                null, turn.Role == TranscriptRole.Assistant, turn.At, null, null, turn.Text));

    /// <summary>The open proposal and its drafts; null when none is open.</summary>
    public TicketConversationProposalView? Proposal(SpecDialogSession session)
    {
        if (session.LatestProposalJson is not { } json) return null;
        var ratified = Read(session, () => RatifiedMocks.Of(SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson)),
            new Dictionary<string, int>());
        return Read<TicketConversationProposalView?>(session, () => Of(OutcomeProposalJson.Read(json), ratified), null);
    }

    private static TicketConversationProposalView Of(OutcomeProposal proposal, IReadOnlyDictionary<string, int> ratified) => (proposal switch
    {
        SpecOutcome spec => new TicketConversationProposalView("spec", [Draft(spec.Draft, ratified)]),
        EpicOutcome epic => new("epic", [Draft(epic.Parent, ratified), .. epic.Children.Select(c => Draft(c, ratified))]),
        _ => new("bug", []),
    }) with { Findings = proposal.Findings, ReviewRounds = proposal.ReviewRounds, NotReviewed = proposal.NotReviewed };

    private static TicketConversationDraftView Draft(SpecDraft draft, IReadOnlyDictionary<string, int> ratified) =>
        new(draft.SpecId, draft.Goal, draft.Repos, draft.Done, ratified.TryGetValue(draft.SpecId, out var v) ? v : null);

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
