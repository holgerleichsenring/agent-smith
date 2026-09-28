using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Init;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0489: composition for the manual init launch behind POST /api/projects/{name}/init and
/// the chat init command. Everything here is SCOPED: the launcher reads and writes run rows
/// over the scoped unit of work. Both doors register it, so each registration is a TryAdd.
/// </summary>
internal static class ProjectInitExtensions
{
    internal static IServiceCollection AddProjectInit(this IServiceCollection services)
    {
        services.TryAddScoped<InitRunRepository>();
        services.TryAddScoped<InitRunAdmission>();
        services.TryAddScoped<InitRunLauncher>();
        return services;
    }
}
