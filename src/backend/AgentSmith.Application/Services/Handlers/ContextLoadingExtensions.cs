using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-04-cf3d: the two loaders that hand the master a repository's context.yaml and
/// principles.md, and the reader they share to walk every context of a sandbox.
/// </summary>
public static class ContextLoadingExtensions
{
    public static IServiceCollection AddContextLoading(this IServiceCollection services)
    {
        services.AddTransient<ContextDocumentReader>();
        services.AddTransient<ICommandHandler<LoadContextContext>, LoadContextHandler>();
        services.AddTransient<ICommandHandler<LoadCodingPrinciplesContext>, LoadCodingPrinciplesHandler>();
        return services;
    }
}
