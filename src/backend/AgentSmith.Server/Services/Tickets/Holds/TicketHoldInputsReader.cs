using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Preconditions;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.Tickets.Text;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>
/// 2026-11-23-31dcg: the hold inputs of one read, in a fixed number of reads whatever the number of tickets — one series-run
/// read for the scope, and per project the series page's own set-based facts (<see cref="SeriesRunProjectReads"/>) and its
/// merge order (<see cref="SeriesRunTickets"/>), so a ticket's merge hold is the order the series page shows; one ledger
/// read for the open text edits. On a scope of its own: the readers that call it are transient.
/// </summary>
public sealed class TicketHoldInputsReader(IServiceScopeFactory scopes, InterruptedRestartWindow? restart = null)
{
    /// <param name="ticketId">One ticket's inputs (the ticket page); null for every ticket of the scope.</param>
    public async Task<TicketHoldInputs> ReadAsync(
        ProjectScope scope, IReadOnlyDictionary<string, ResolvedProject> configured, PreconditionsConfig? preconditions,
        string? ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(configured);
        using var services = scopes.CreateScope();
        var (series, steps) = await SeriesAsync(services.ServiceProvider, scope, configured, ct);
        var edited = await EditedAsync(services.ServiceProvider, scope, ticketId, ct);
        var since = preconditions is null || restart is null ? null : restart.Since(preconditions);
        return new TicketHoldInputs(series, steps, edited, since);
    }

    // The newest series run that lists a ticket decides its series and its step; older ones it was in say nothing.
    private static async Task<(Dictionary<(string, string), string>, Dictionary<(string, string), SeriesRunMergeStep>)> SeriesAsync(
        IServiceProvider services, ProjectScope scope, IReadOnlyDictionary<string, ResolvedProject> configured, CancellationToken ct)
    {
        var series = new Dictionary<(string, string), string>();
        var steps = new Dictionary<(string, string), SeriesRunMergeStep>();
        var records = (await services.GetRequiredService<ISeriesRunStore>().ListAsync(scope.Only, ct))
            .Where(r => scope.Admits(r.Project) && configured.ContainsKey(r.Project)).ToList();
        if (records.Count == 0) return (series, steps);
        var reads = services.GetRequiredService<SeriesRunProjectReads>();
        var tickets = services.GetRequiredService<SeriesRunTickets>();
        foreach (var project in records.GroupBy(r => r.Project, StringComparer.Ordinal))
        {
            var facts = await reads.ReadAsync(configured[project.Key], [.. project.SelectMany(r => r.Tickets)], ct);
            foreach (var record in project.OrderBy(r => r.StartedAt))
            {
                var order = tickets.Of(record, facts).MergeOrder.ToDictionary(s => s.TicketId, StringComparer.Ordinal);
                foreach (var ticket in record.Tickets)
                {
                    var key = (project.Key, ticket.TicketId);
                    series[key] = record.Id;
                    if (order.TryGetValue(ticket.TicketId, out var step)) steps[key] = step;
                    else steps.Remove(key);
                }
            }
        }
        return (series, steps);
    }

    private static async Task<HashSet<(string, string)>> EditedAsync(
        IServiceProvider services, ProjectScope scope, string? ticketId, CancellationToken ct)
    {
        var rows = await services.GetRequiredService<ReworkLedgerRepository>()
            .WithKeyPrefixAsync(TicketTextEditNotice.KeyPrefix, scope.Only, ticketId, ct);
        return [.. rows.Where(r => scope.Admits(r.Project) && TicketTextEditNotice.Open(r.Keys).Count > 0).Select(r => (r.Project, r.TicketId))];
    }
}
