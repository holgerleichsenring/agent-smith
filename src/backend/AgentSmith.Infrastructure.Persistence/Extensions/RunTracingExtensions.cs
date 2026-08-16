using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>
/// p0423: the durable trace writer, replacing the null default the loop runtime registers.
/// A traced run's conversation lands in the artifact store beside the run record, so it
/// outlives the ephemeral container that produced it — the one case where a trace written
/// to a local filesystem is guaranteed to be gone when it is needed.
/// </summary>
public static class RunTracingExtensions
{
    public static IServiceCollection AddRunTracing(this IServiceCollection services)
    {
        services.TryAddScoped<RunArtifactRepository>();
        services.AddSingleton<IRunTraceWriter, RunTraceWriter>();
        // p0427: a record nobody can read back is a record nobody can replay.
        services.AddSingleton<IRunTraceReader, RunTraceReader>();
        return services;
    }
}
