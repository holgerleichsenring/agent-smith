using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-27-3532a: a ticket a tracker triggered enters its series — the series that holds it, or a series of one opened for it
/// (<see cref="SeriesOfOne"/>) — and its master is woken once per series, ticket and latest attempt (a Prerequisite-kind wake,
/// once-key <c>start:{series}:{ticket}:{attempt}</c>) to start it. A ticket a run worked already is said so: retry_ticket runs it again. A ticket
/// whose newest series was stopped is not reopened by a trigger (a poll cannot tell a person's tag from a re-poll, and a budget
/// stop must hold): a person starts it by an act. The poller and the webhook dispatcher resolve the funnel from the root, while
/// the series services are scoped, so the entry opens a scope of its own; a process with no series store answers null.
/// </summary>
public sealed class SeriesTicketEntry(IServiceScopeFactory scopes, ILogger<SeriesTicketEntry> logger) : ISeriesTicketEntry
{
    public async Task<string?> EnterAsync(ResolvedProject project, string ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        if (services.GetService<ISeriesRunStore>() is not { } store || services.GetService<SeriesOfOne>() is not { } one) return null;
        var id = await HolderAsync(store, project.Name, ticketId, cancellationToken)
            ?? await one.OpenAsync(project, ticketId, null, cancellationToken)
            ?? await HolderAsync(store, project.Name, ticketId, cancellationToken); // another opener won the same id
        if (id is null) return Stopped(project.Name, ticketId);
        var attempt = services.GetService<IPreviousAttemptReader>() is { } attempts
            ? (await attempts.LatestAsync(project.Name, ticketId, null, cancellationToken))?.RunId : null;
        await services.GetRequiredService<ISeriesWakes>().WakeAsync(project.Name, ticketId, Wake(id, ticketId, attempt), cancellationToken);
        return $"series {id} leads it; its master was woken to start it";
    }

    /// <summary>The wake a ticket's entry into series <paramref name="seriesId"/> records — once per series, ticket and latest
    /// attempt (<paramref name="attempt"/>, its run id; null when no run worked it).</summary>
    public static SeriesWake Wake(string seriesId, string ticketId, string? attempt) => attempt is null
        ? SeriesStartWakes.For(seriesId, ticketId, "the tracker triggered it; it waits for start_ticket")
        : SeriesStartWakes.For(seriesId, ticketId, $"the tracker triggered it again; its run {attempt} worked it already, so "
            + "retry_ticket runs it again", attempt);

    private static async Task<string?> HolderAsync(ISeriesRunStore store, string project, string ticketId, CancellationToken ct) =>
        (await store.ListAsync([project], ct)).FirstOrDefault(r => r.IsActive && r.Tickets.Any(t => t.TicketId == ticketId))?.Id;

    private string Stopped(string project, string ticketId)
    {
        logger.LogInformation("Trigger of {Project}/{Ticket}: its series was stopped; a person starts it", project, ticketId);
        return "its series was stopped, so no trigger reopens it; a person starts it (Retry, or Start on its series node)";
    }
}
