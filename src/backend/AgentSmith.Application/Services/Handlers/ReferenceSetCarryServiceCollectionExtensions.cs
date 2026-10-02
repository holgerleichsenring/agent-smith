using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-01-283df: the step that carries the approved websites into a run, and the two services
/// it is composed of. Its own extension, like the target probe's, so no baseline file grows.
/// </summary>
public static class ReferenceSetCarryServiceCollectionExtensions
{
    public static IServiceCollection AddReferenceSetCarry(this IServiceCollection services)
    {
        services.AddTransient<ReferenceGitExclusion>();
        services.AddTransient<ReferenceSetCarrier>();
        services.AddTransient<ICommandHandler<MaterializeReferenceSetsContext>, MaterializeReferenceSetsHandler>();
        return services;
    }
}
