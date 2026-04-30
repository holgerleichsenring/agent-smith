using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Decorating factory that wraps the Jira branch of the inner factory in
/// LockedTicketStatusTransitioner. Registered only in the Server composition;
/// CLI uses the plain TicketStatusTransitionerFactory directly.
/// </summary>
public sealed class LockingTicketStatusTransitionerFactory(
    TicketStatusTransitionerFactory inner,
    IRedisClaimLock labelLock,
    ILoggerFactory loggerFactory) : ITicketStatusTransitionerFactory
{
    public ITicketStatusTransitioner Create(TicketConfig config)
    {
        var transitioner = inner.Create(config);
        return string.Equals(config.Type, "jira", StringComparison.OrdinalIgnoreCase)
            ? new LockedTicketStatusTransitioner(
                transitioner, labelLock,
                loggerFactory.CreateLogger<LockedTicketStatusTransitioner>())
            : transitioner;
    }
}
