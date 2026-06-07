using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Decorates the platform transitioner factory so every created transitioner is
/// DB-authoritative (p0246d). Captures the platform from the tracker; the project
/// key is left empty here (the transition call site does not thread it), which
/// the (Project, Platform, TicketId) unique index still satisfies.
/// </summary>
public sealed class DbAuthoritativeTransitionerFactory(
    ITicketStatusTransitionerFactory inner,
    IServiceScopeFactory scopeFactory,
    ILoggerFactory loggerFactory) : ITicketStatusTransitionerFactory
{
    public ITicketStatusTransitioner Create(TrackerConnection config) =>
        new DbAuthoritativeTicketStatusTransitioner(
            inner.Create(config),
            scopeFactory,
            project: string.Empty,
            platform: config.Type.ToString(),
            loggerFactory.CreateLogger<DbAuthoritativeTicketStatusTransitioner>());
}
