using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Whether a bound turn may start its ticket's next attempt, and the port when it may: only for a poster who
/// holds runs.control and a ticket that has an attempt to continue. Every other turn gets no port, so the
/// agent is never offered a start it cannot make.
/// </summary>
public sealed class BoundAttemptStarts(
    IPreviousAttemptReader attempts, IServiceScopeFactory scopes, ILoggerFactory loggerFactory)
{
    public async Task<IAttemptStart?> ForAsync(
        ResolvedProject project, string ticketId, string conversation, TurnPoster poster, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(poster);
        if (!poster.MayStartRuns || await attempts.LatestAsync(project.Name, ticketId, null, ct) is null) return null;
        return new ConversationAttemptStart(scopes, project, ticketId, conversation, poster.Id,
            loggerFactory.CreateLogger<ConversationAttemptStart>());
    }
}
