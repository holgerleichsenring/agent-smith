using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services.DataMigrations;
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
        services.AddSingleton<KeptSetCarryBackfill>(); // 2026-11-22-a720a
        services.AddSingleton<DeployDeliveredAtBackfill>(); // 2026-11-23-af60
        services.AddSingleton<SchemaWatermark>(); // 2026-11-22-747da: data migrations, run once and recorded
        services.AddSingleton<DataMigrationLedger>();
        services.AddSingleton<DataMigrationRunner>();
        services.AddSingleton<RunStoreMigrator>();
        services.AddStoredSpecNameRewrite();
        // In this order: the rewrite moves names first, the backfill reads events in today's shape.
        services.AddSingleton<IDataMigration>(sp => sp.GetRequiredService<StoredSpecNameRewrite>());
        services.AddSingleton<IDataMigration>(sp => sp.GetRequiredService<KeptSetCarryBackfill>());
        services.AddSingleton<IDataMigration>(sp => sp.GetRequiredService<DeployDeliveredAtBackfill>());
        return services;
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
        services.AddSingleton<IStoredSpecNameSurface, SpecDialogProposalSpecNames>(); // 2026-11-17-a0b6d
        services.AddSingleton<IStoredSpecNameSurface, RunStepSpecNames>();
        services.AddSingleton<StoredSpecNameRewrite>();
        return services;
    }
}
