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
/// A one-shot run has no server to drain anything, so it projects into its own store —
/// where the record outlives the process. The traced CONVERSATION goes to the database
/// too; it is far too large for a run stream, where a build's output once rolled the
/// retained window over and collapsed the trail (p0373).
/// </para>
/// </summary>
internal static class CliRunRecordingRegistration
{
    public static void AddCliRunRecording(this IServiceCollection services)
    {
        services.AddSingleton<CliRunStore>();
        services.AddRunRecording(sp => sp.GetRequiredService<CliRunStore>().Options);
        services.AddScoped<CliRunRecordingSchema>();
        services.AddRunTracing();

        services.AddSingleton<ProjectingEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => new PreparedStoreEventPublisher(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<CliRunStore>(),
            sp.GetRequiredService<ProjectingEventPublisher>()));
    }
}
