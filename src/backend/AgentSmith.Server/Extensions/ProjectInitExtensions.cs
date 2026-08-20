using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Init;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0489: composition for the manual init launch behind POST /api/projects/{name}/init.
/// Everything here is SCOPED: the launcher reads and writes run rows over the scoped
/// unit of work, and its only caller is one HTTP request.
/// </summary>
internal static class ProjectInitExtensions
{
    internal static IServiceCollection AddProjectInit(this IServiceCollection services)
    {
        services.AddScoped<InitRunRepository>();
        services.AddScoped<InitRunAdmission>();
        services.AddScoped<InitRunLauncher>();
        return services;
    }
}
