using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The series ports a bound turn is seeded with: the read for a ticket a code attempt worked, and the edit beside it
/// only for a poster who holds runs.control. A never-worked ticket's series is formed in Work it out, so its turn
/// gets neither.
/// </summary>
public sealed class BoundSeriesPorts(IPreviousAttemptReader attempts, IServiceScopeFactory scopes)
{
    public async Task<BoundSeries?> ForAsync(
        ResolvedProject project, string ticketId, string conversation, TurnPoster poster, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(poster);
        if (await attempts.LatestAsync(project.Name, ticketId, null, ct) is null) return null;
        return new BoundSeries(new ConversationSeriesRead(scopes, project, ticketId),
            poster.MayStartRuns ? new ConversationSeriesEdit(scopes, project, ticketId, conversation, poster.Id) : null);
    }
}
