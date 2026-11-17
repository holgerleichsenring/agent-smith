using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-11-17-713ec: the branch-cleanup preset's one step.</summary>
public static class BranchCleanupHandlersExtensions
{
    public static IServiceCollection AddBranchCleanupHandlers(this IServiceCollection services) =>
        services.AddTransient<ICommandHandler<CleanupWorkBranchesContext>, CleanupWorkBranchesHandler>();
}
