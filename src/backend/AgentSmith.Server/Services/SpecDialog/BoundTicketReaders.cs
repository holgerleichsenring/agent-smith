using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-27-481ba: builds the ticket reader a BOUND conversation's turn is seeded with, and
/// answers null for an unbound one — which is what keeps the tool off an unbound turn.
/// <para>
/// A reader needs the project AND the ticket id. The stored row carries the id and no tracker, and
/// the turn carries the project; neither alone can reach a board, so the pairing happens here
/// rather than in the runner, which sits at the per-file limit.
/// </para>
/// </summary>
public sealed class BoundTicketReaders(
    ITicketProviderFactory providers,
    SpecDialogTicketTextRepository store,
    FiledWorkRunsReader runs,
    TicketRunsFacts facts,
    BoundAttemptStarts starts,
    BoundSeriesPorts series,
    Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopes,
    ILoggerFactory loggerFactory)
{
    private static FrameworkFacts Facts(ResolvedProject project, string ticketId)
    {
        var labels = TicketLabelVocabulary.For(project.Tracker);
        return new FrameworkFacts(
            TicketBranchNamer.Compose(new TicketId(ticketId)).Value,
            labels.For(TicketLifecycleStatus.Enqueued),
            labels.For(TicketLifecycleStatus.InProgress),
            labels.ApprovedSetStamp);
    }

    public async Task<BoundTicket?> ForAsync(
        string sessionId, ResolvedProject? project, TurnPoster poster, CancellationToken ct)
    {
        if (project is null) return null;
        var held = await store.GetAsync(sessionId, ct);
        if (held is null) return null;
        return new BoundTicket(
            new BoundTicketReader(
                providers, project, held.TicketId, loggerFactory.CreateLogger<BoundTicketReader>()),
            // 2026-09-28-1da5c: and what the TRACKER shows against it. One lookup, two ports —
            // the pairing that made this class exist is the same pairing both of them need.
            new BoundTicketWork(
                providers.CreateLinkedWork(project.Tracker), new TicketId(held.TicketId)),
            // 2026-09-28-1da5d: and what THIS framework did about it, which is a different claim.
            new BoundTicketRuns(
                runs, facts, project, held.TicketId, loggerFactory.CreateLogger<BoundTicketRuns>()),
            // 2026-09-28-1da5e: and what this framework's own rules produce for this ticket,
            // taken from the code that decides them rather than restated anywhere.
            Facts(project, held.TicketId),
            // And starting its next attempt — as the poster, only when the poster could.
            await starts.ForAsync(project, held.TicketId, sessionId, poster, ct),
            // And its series as the next attempt will read it — editable as the poster, only when the poster could.
            await series.ForAsync(project, held.TicketId, sessionId, poster, ct))
        {
            // 2026-10-26-bdd0g: and deriving its specs without an attempt — as the poster, only when the poster could.
            Derive = poster.MayStartRuns
                ? new ConversationSpecDerivation(scopes, project, held.TicketId, sessionId, poster.Id,
                    loggerFactory.CreateLogger<ConversationSpecDerivation>())
                : null,
        };
    }
}

/// <summary>What a BOUND turn may ask about its own ticket, from one lookup — and the start and series edit its poster may make.</summary>
public sealed record BoundTicket(
    ITicketReader Reader, IBoundTicketWork Work, IBoundTicketRuns Runs, FrameworkFacts Facts, IAttemptStart? Start = null,
    BoundSeries? Series = null)
{
    /// <summary>2026-10-26-bdd0g: deriving the ticket's specs as the poster; null for a poster who may not start runs.</summary>
    public ISpecDerivationStart? Derive { get; init; }

    /// <summary>A code attempt worked the ticket — the one predicate the series ports are built on, so the steward
    /// leads exactly the turns that can read the series.</summary>
    public bool Worked => Series is not null;
}

/// <summary>2026-09-28-1da5c: the linked-work port, closed over the ticket the turn is bound to.</summary>
public sealed class BoundTicketWork(ITicketLinkedWork work, TicketId ticketId) : IBoundTicketWork
{
    public Task<TicketLinkedWorkResult> ForAsync(CancellationToken cancellationToken) =>
        work.ForAsync(ticketId, cancellationToken);
}
