using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Server.Services.Tickets;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The bound ticket's series as its next attempt will read it (<see cref="NextAttemptSeriesReader"/>), each spec with
/// its state in the cut it came from and whether it ran. Each read runs in its own scope, apart from the turn's unit
/// of work.
/// </summary>
public sealed class ConversationSeriesRead(IServiceScopeFactory scopes, ResolvedProject project, string ticketId) : ISeriesRead
{
    public async Task<SeriesReading> ReadAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var next = await scope.ServiceProvider.GetRequiredService<NextAttemptSeriesReader>()
            .ReadAsync(project, ticketId, mayStartRuns: false, cancellationToken);
        if (next.Read.State is not { } state) return SeriesReading.Failed(next.Read.Failure!);
        var states = state.Basis.GroupBy(b => b.SpecId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().State, StringComparer.Ordinal);
        var ran = state.ExecutedIds.ToHashSet(StringComparer.Ordinal);
        return new SeriesReading(null, state.Series,
            [.. next.Specs.Select(d => new SeriesEntry(
                d, states.GetValueOrDefault(d.SpecId) ?? TicketSeriesCuts.NotStarted, ran.Contains(d.SpecId)))],
            state.Scope.Shas ?? new Dictionary<string, string>());
    }
}
