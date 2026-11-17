using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-11-17-713ec: the branch-cleanup preset's one step; 2026-11-17-023fb: the refresh-base preset's.</summary>
public static class BranchCleanupHandlersExtensions
{
    public static IServiceCollection AddBranchCleanupHandlers(this IServiceCollection services) =>
        services.AddTransient<ICommandHandler<CleanupWorkBranchesContext>, CleanupWorkBranchesHandler>()
            .AddTransient<ICommandHandler<RefreshBranchBaseContext>, RefreshBranchBaseHandler>(); // 2026-11-17-023fb
}
