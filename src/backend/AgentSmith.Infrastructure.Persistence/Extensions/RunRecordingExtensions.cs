using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Interceptors;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>
/// p0423: the smallest graph that lets a run write itself down — a DbContext, the event
/// applier with its projections, the trail projector, and a publisher that feeds it.
/// <para>
/// This is what the CLI adds. It is deliberately NOT the server's persistence graph: no
/// lease, no capacity queue, no config store, no hosted services. A one-shot run needs to
/// be READ afterwards, not coordinated with other replicas.
/// </para>
/// </summary>
public static class RunRecordingExtensions
{
    public static IServiceCollection AddRunRecording(
        this IServiceCollection services, Func<IServiceProvider, PersistenceOptions> resolveOptions)
    {
        services.AddDbContext<AgentSmithDbContext>((sp, b) =>
        {
            var options = resolveOptions(sp);
            b.UseProvider(options);
            if (options.Provider == PersistenceProvider.Sqlite)
                b.AddInterceptors(new SqliteTuningInterceptor(
                    sp.GetRequiredService<ILogger<SqliteTuningInterceptor>>()));
        }, ServiceLifetime.Scoped);
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AgentSmithDbContext>());
        services.TryAddSingleton(TimeProvider.System);
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton<RunDbProjector>();
        // Last binding wins over the NoOp default the core chain registers.
        services.AddSingleton<IEventPublisher, ProjectingEventPublisher>();
        return services;
    }
}
