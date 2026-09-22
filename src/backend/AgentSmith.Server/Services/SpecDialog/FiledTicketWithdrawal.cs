using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-22-9519: the way back out of a filing. One ticket, named by the display key the filing
/// reported, closed on the tracker and then — and only then — recorded as withdrawn.
/// <para>
/// THE ORDER IS THE CLAIM, THEN THE CLOSE, THEN THE RECORD. What a claim means is
/// <see cref="FiledWorkClaims"/>'s subject. THE RECORD FOLLOWS THE TRACKER: the close answers
/// whether it closed, and a close that did not land changes nothing — no state, no nudge, no claim
/// in the reply. The alternative is the one failure this path exists to prevent, a filing that
/// says withdrawn about a ticket still sitting on the board.
/// </para>
/// <para>Its own scope: the read, the claim questions and the write are a unit of work of their
/// own, opened while a turn is mid-flight over the scope serving the operator's message.</para>
/// </summary>
public sealed class FiledTicketWithdrawal(
    IServiceScopeFactory scopeFactory,
    AgentSmithConfig config,
    ITicketProviderFactory ticketFactory,
    FiledWorkTrackerProjects trackerProjects,
    FiledWorkNudge? nudge,
    ILogger<FiledTicketWithdrawal> logger) : IFiledTicketWithdrawal
{
    public async Task<FiledTicketWithdrawalResult> WithdrawAsync(
        string sessionId, string displayKey, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(displayKey))
            return Refused("Name the ticket to withdraw by the key this conversation filed it under.");
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<SpecDialogLatestOutcomeStore>();
        var filing = (await store.ReadBySessionAsync(sessionId, cancellationToken)).Filing;
        if (filing is null)
            return Refused("This conversation has filed nothing, so there is nothing to withdraw.");

        var named = filing.Filed.Where(t => t.Key is not null).ToList();
        if (named.Count == 0)
            return Refused(
                "This filing was written before filed tickets carried a display key, so no ticket "
                + "on it can be named — close it in the tracker instead.");
        var ticket = named.FirstOrDefault(
            t => string.Equals(t.Key, displayKey, StringComparison.OrdinalIgnoreCase));
        if (ticket is null)
            return Refused(
                $"This conversation filed no ticket '{displayKey}'. It filed: "
                + $"{string.Join(", ", named.Select(t => t.Key))}.");
        if (ticket.TicketId is not { } ticketId || ticket.Project is not { } projectName)
            return Refused(
                $"{displayKey} was filed before its id and project were recorded, so this cannot "
                + "reach its tracker — close it there instead.");
        if (!config.Projects.TryGetValue(projectName, out var project))
            return Refused(
                $"Project '{projectName}' is no longer in the configuration catalog, so "
                + $"{displayKey}'s tracker cannot be reached from here.");

        var reach = trackerProjects.SharingTrackerWith(projectName);
        if (await FiledWorkClaims.OnAsync(scope, reach, ticketId, cancellationToken) is { } claimed)
            return Refused($"{displayKey} {claimed} Stop the run from the run controls instead.");

        return await CloseAsync(
            store, project, filing, ticket, sessionId, displayKey, ticketId, reason, cancellationToken);
    }

    private async Task<FiledTicketWithdrawalResult> CloseAsync(
        SpecDialogLatestOutcomeStore store, ResolvedProject project, SpecDialogFiling filing,
        FiledTicket ticket, string sessionId, string displayKey, string ticketId, string reason,
        CancellationToken ct)
    {
        bool closed;
        try
        {
            closed = await ticketFactory.Create(project.Tracker)
                .CloseTicketAsync(new TicketId(ticketId), Resolution(reason), ct);
        }
        // A tracker that threw closed nothing, and the conversation is told rather than the turn failing.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Withdrawing filed ticket {Ticket} threw", ticketId);
            return Refused($"{displayKey} could not be closed: {ex.Message}");
        }
        if (!closed)
        {
            logger.LogInformation(
                "The tracker did not close filed ticket {Ticket}; its filing record is unchanged", ticketId);
            return Refused(
                $"{project.Tracker.Type} did not close {displayKey} — it is still open and the "
                + "filing record is unchanged.");
        }

        await store.SetFilingBySessionAsync(sessionId, Stamped(filing, ticket, Withdrawn(reason)), ct);
        // The open pane learns by the key its watch is registered under, which is the ticket.
        if (nudge is not null) await nudge.OfTicketAsync(ticketId, ct);
        logger.LogInformation("Filed ticket {Ticket} was withdrawn by the conversation that filed it", ticketId);
        return new FiledTicketWithdrawalResult(
            true, $"{displayKey} is closed, and this conversation's filing now records it as withdrawn.");
    }

    private static FiledWorkStart Withdrawn(string reason) =>
        new(FiledStartState.Withdrawn, $"closed from the conversation that filed it{Because(reason)}");

    private static string Resolution(string reason) =>
        $"Withdrawn by the design conversation that filed it{Because(reason)}";

    private static string Because(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? "." : $": {reason}";

    /// <summary>The whole filing is rewritten with ONE ticket's state replaced: its tickets, error,
    /// moment, kind and notes all survive a withdrawal unchanged.</summary>
    private static SpecDialogFiling Stamped(SpecDialogFiling f, FiledTicket t, FiledWorkStart start) =>
        f with { Filed = [.. f.Filed.Select(x => ReferenceEquals(x, t) ? x with { Start = start } : x)] };

    private static FiledTicketWithdrawalResult Refused(string reason) => new(false, reason);
}
