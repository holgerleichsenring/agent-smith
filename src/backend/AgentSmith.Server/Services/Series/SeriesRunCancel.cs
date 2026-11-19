using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.Lifecycle;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-19-1b72a: a series' "stop now" cancels a member ticket's run through the cancel a person's Cancel
/// button takes — the same persisted flag, enforcer, queue removal and ticket finalize — never a second cancel. The
/// cancel's services are read when a cancel is asked for, so a beat that cancels nothing never builds them.</summary>
public sealed class SeriesRunCancel(IServiceProvider services)
{
    public async Task<string> CancelAsync(string runId, CancellationToken cancellationToken)
    {
        var result = await RunControlEndpoints.CancelAsync(runId, services.GetRequiredService<IRunCancellationRegistry>(),
            services.GetRequiredService<JobsBroadcaster>(), services.GetRequiredService<IEventPublisher>(),
            services.GetRequiredService<RunRepository>(), services.GetRequiredService<ICapacityQueue>(),
            services.GetRequiredService<CancelledTicketFinalizer>(), services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<CancelTerminalWriter>(), cancellationToken, services.GetService<IReworkWatermark>());
        return result is NotFound ? $"Run {runId} was not found to cancel." : $"Run {runId} is cancelled.";
    }
}
