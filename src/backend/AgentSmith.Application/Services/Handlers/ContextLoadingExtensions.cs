using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-04-cf3d: the two loaders that hand the master a repository's context.yaml and
/// principles.md, and the reader they share to walk every context of a sandbox.
/// 2026-09-19-4c1f: and the third, which reads the same two files REMOTELY for a design
/// turn that has no sandbox to read them from and must provision none.
/// </summary>
public static class ContextLoadingExtensions
{
    public static IServiceCollection AddContextLoading(this IServiceCollection services)
    {
        services.AddTransient<ContextDocumentReader>();
        services.AddTransient<ICommandHandler<LoadContextContext>, LoadContextHandler>();
        services.AddTransient<ICommandHandler<LoadCodingPrinciplesContext>, LoadCodingPrinciplesHandler>();
        services.AddTransient<DialogGroundingReader>();
        services.AddTransient<ICommandHandler<GroundSpecDialogContext>, GroundSpecDialogHandler>();
        return services;
    }
}
