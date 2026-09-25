using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>
/// 2026-09-25-b4d9: the two standing facts a server keeps about a ticket — that a run could
/// not move it out of its trigger status (c1a7), and that this framework took it up and has
/// not finished it. Both are in-memory by default and relational here, and both follow the
/// same shape: a scoped repository doing the work, a singleton facade opening a scope per op.
/// </summary>
public static class TicketFactStoreExtensions
{
    public static IServiceCollection AddTicketFactStores(this IServiceCollection services)
    {
        services.AddScoped<UnmovedTicketRepository>().AddScoped<TakenTicketRepository>();
        services.RemoveAll<IUnmovedTicketStore>().RemoveAll<ITakenTicketStore>();
        return services
            .AddSingleton<IUnmovedTicketStore, DbUnmovedTicketStore>()
            .AddSingleton<ITakenTicketStore, DbTakenTicketStore>();
    }
}
