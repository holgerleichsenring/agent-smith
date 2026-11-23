using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Pocs;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-15-4a4db: the project scope of the routes, registered with the caller identity it reads —
/// unconditionally, so the route table enumerates without an authority. The middleware itself is put in
/// the pipeline only by an enforcing installation.
/// </summary>
internal static class ProjectScopeExtensions
{
    internal static IServiceCollection AddProjectScope(this IServiceCollection services)
    {
        services.TryAddTransient<TicketProjects>();
        services.AddSingleton<ProjectAccess>();
        services.AddSingleton<CallerProjectScope>().AddSingleton<ProjectScopedCatalog>(); // 2026-10-15-4a4dd
        services.AddScoped<ResourceProjectReader>();
        services.AddScoped<ProjectSourceResolver>();
        services.AddScoped<DialogMessageScope>();
        // 2026-11-23-933da: the pocs switch, the act rule and the list.
        services.AddTransient<PocsGate>().AddSingleton<PocActAccess>().AddScoped<PocListReader>();
        return services;
    }
}
