using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services.Repair;
using AgentSmith.Infrastructure.Persistence.Services.SpecRename;
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
        return services.AddStoredSpecNameRewrite();
    }

    // 2026-10-12-967ab: the one-time rewrite of rows stored in the pre-rename names, run by the migrator.
    private static IServiceCollection AddStoredSpecNameRewrite(this IServiceCollection services)
    {
        services.AddSingleton<SpecMemberRewriter>();
        services.AddSingleton<CheckpointEntryNames>();
        services.AddSingleton<SpecRenameJson>();
        services.AddSingleton<SpecApprovalJsonRewriter>();
        services.AddSingleton<CheckpointJsonRewriter>();
        services.AddSingleton<RequestContextJsonRewriter>();
        services.AddSingleton<EventPayloadRewriter>();
        services.AddSingleton<SpecDialogJsonRewriter>();
        services.AddSingleton<RowPages>();
        services.AddSingleton<IStoredSpecNameSurface, RunEventSpecNames>();
        services.AddSingleton<IStoredSpecNameSurface, RunCheckpointSpecNames>();
        services.AddSingleton<IStoredSpecNameSurface, RunArtifactSpecNames>();
        services.AddSingleton<IStoredSpecNameSurface, RunSpecNames>();
        services.AddSingleton<IStoredSpecNameSurface, QueuedTicketSpecNames>();
        services.AddSingleton<IStoredSpecNameSurface, ApprovedSeriesSpecNames>();
        services.AddSingleton<IStoredSpecNameSurface, SpecDialogSessionSpecNames>();
        services.AddSingleton<IStoredSpecNameSurface, RunStepSpecNames>();
        services.AddSingleton<StoredSpecNameRewrite>();
        return services;
    }
}
