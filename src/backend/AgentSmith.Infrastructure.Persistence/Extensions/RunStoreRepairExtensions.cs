using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Repair;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>
/// 2026-08-25-61f1: the repair that has to run before the store can be constrained, and the
/// migrator that guarantees it does. Registered as one seam because they are one piece of
/// work: the constraint and the removal of the rows it would reject cannot be separated
/// without shipping a migration that cannot run.
/// </summary>
public static class RunStoreRepairExtensions
{
    public static IServiceCollection AddRunStoreRepair(this IServiceCollection services)
    {
        services.AddSingleton<ReplayedRunFinder>();
        services.AddSingleton<ReplayedRunRows>();
        services.AddSingleton<DuplicateRowSelector>();
        services.AddSingleton<RunCostRecomputer>();
        services.AddSingleton<RunDuplicateRepair>();
        services.AddSingleton<RunStoreMigrator>();
        return services;
    }
}
