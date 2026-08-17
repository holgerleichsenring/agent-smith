using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Events;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Services;

/// <summary>
/// p0423: a run records itself, wherever it runs.
/// <para>
/// The CLI's publisher was <c>NoOpEventPublisher</c>, so nothing a CLI run did was ever
/// written down — twelve hours of live debugging against a run database of zero bytes,
/// and every question cost another run.
/// </para>
/// <para>
/// Two destinations, one rule: write where the record OUTLIVES the process. A spawned job
/// runs in an ephemeral container, so its events go up the Redis stream the server already
/// drains and projects; a local one-shot has no server to drain anything and projects into
/// its own store. The traced CONVERSATION goes to the database in both cases — it is far
/// too large for the run stream, where a build's output once rolled the retained window
/// over and collapsed the trail (p0373).
/// </para>
/// </summary>
internal static class CliRunRecordingRegistration
{
    public static void AddCliRunRecording(
        this IServiceCollection services, string jobId, string redisUrl)
    {
        services.AddSingleton<CliRunStore>();
        services.AddRunRecording(sp => sp.GetRequiredService<CliRunStore>().Options);
        services.AddScoped<CliRunRecordingSchema>();
        services.AddRunTracing();

        if (IsSpawnedJob(jobId, redisUrl))
        {
            services.AddSingleton<EventEnvelopeSerializer>();
            services.AddSingleton<IEventPublisher, RedisEventPublisher>();
            return;
        }

        services.AddSingleton<ProjectingEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => new PreparedStoreEventPublisher(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<CliRunStore>(),
            sp.GetRequiredService<ProjectingEventPublisher>()));
    }

    private static bool IsSpawnedJob(string jobId, string redisUrl) =>
        !string.IsNullOrWhiteSpace(jobId) && !string.IsNullOrWhiteSpace(redisUrl);
}
