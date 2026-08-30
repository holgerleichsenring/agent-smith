using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-3c12: the surface a master pass runs on — the catalogue of tool sets and the
/// composer that decides which of them this pass is given. They are registered together
/// because they change together: every phase that adds a tool to a master touches the
/// composer, and none of them touches the pipeline's handler registrations.
/// </summary>
public static class ToolRegistrations
{
    public static IServiceCollection AddMasterToolSurface(this IServiceCollection services)
    {
        services.AddSingleton<AgenticToolSurface>();
        services.AddTransient<MasterToolComposer>();
        return services;
    }
}
