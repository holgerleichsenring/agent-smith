using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Interceptors;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Services.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0246g: ONE persistence path. The SERVER always wires the relational store
/// from config.persistence (sqlite default) + Redis (transport/locks/nudges) —
/// not a toggle. The CLI one-shot runs never call this (no DB). Migrations are
/// applied explicitly by `agentsmith database migrate` (an init-container / one-
/// shot service in the deployment), never on startup, so the server assumes the
/// schema is current.
/// </summary>
internal static class RelationalPersistenceExtensions
{
    internal static IServiceCollection AddRelationalPersistence(this IServiceCollection services)
    {
        // The DbContext is SCOPED and IS the unit of work — no IDbContextFactory.
        // Web-request paths get it injected; the background singletons (lease,
        // projector, reaper, retention, transitioner, artifact store) open a scope
        // per operation and resolve a scoped repository. Provider + connection are
        // resolved from config.persistence at build time.
        // p0349: the DbContext connection is bootstrapped from the FILE (BootstrapConfig),
        // NOT from AgentSmithConfig — the server now LOADS AgentSmithConfig from this very
        // DB, so reading persistence off it would be a chicken-and-egg cycle. Persistence +
        // secret names are the one slice that stays in the bootstrap file/env.
        services.TryAddSingleton(sp => sp.GetRequiredService<BootstrapConfigReader>().Read());
        services.AddDbContext<AgentSmithDbContext>(
            (sp, b) =>
            {
                var options = OptionsFrom(sp.GetRequiredService<BootstrapConfig>());
                b.UseProvider(options);
                // A poll query cancelled by its own timeout tears the connection down, and EF's
                // built-in ConnectionError event logs that as Error — a red FAIL for an expected
                // cancellation. Downgrade EF's own event to Warning; the interceptor still raises
                // Error for GENUINE (non-cancelled) connection failures, so real faults stay loud.
                b.ConfigureWarnings(w => w.Log((RelationalEventId.ConnectionError, LogLevel.Warning)));
                // SQLite under concurrent server access needs WAL + a busy timeout, and
                // its connection failures are otherwise logged without detail — both are
                // handled by the interceptor. Other providers don't need it.
                if (options.Provider == PersistenceProvider.Sqlite)
                    b.AddInterceptors(new SqliteTuningInterceptor(
                        sp.GetRequiredService<ILogger<SqliteTuningInterceptor>>()));
            },
            ServiceLifetime.Scoped);
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AgentSmithDbContext>());
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IUniqueViolationTranslator>(sp =>
            TranslatorFor(ProviderOf(sp.GetRequiredService<BootstrapConfig>())));

        // p0349: config is a DB entity-document store. The scoped repositories do the
        // transactional work; the singleton facade opens a scope per op; DbConfigStore
        // is the server's editable IConfigStore (replaces the read-only FileConfigStore),
        // so studio edits PERSIST even under a read-only ConfigMap mount.
        services.AddScoped<ConfigDocumentRepository>();
        services.AddScoped<ConfigImportRepository>();
        services.AddSingleton<IConfigDocumentStore, EfConfigDocumentStore>();
        services.RemoveAll<IConfigStore>();
        services.AddSingleton<ConfigChangeReverter>();
        services.AddSingleton<IConfigStore, DbConfigStore>();
        services.AddScoped<ActiveRunRepository>().AddScoped<ActiveRunLivenessRepository>();
        services.AddScoped<RunArtifactRepository>();
        // p0315a: volatile Redis must never be the only holder of a design transcript; 8e51c: nor
        services.AddScoped<SpecDialogSessionRepository>().AddReferenceFiles() // 2026-10-01-283da
            .AddScoped<SpecDialogTicketTextRepository>(); // of the ticket text it was grounded on

        services.RemoveAll<IActiveRunLease>();
        services.AddSingleton<IActiveRunLease, DbActiveRunLease>();
        services.AddSingleton<StaleLeaseRelease>().AddSingleton<ActiveRunReaper>();

        // p0330: cancel is persistent state — the DB-backed flag reader feeds the pre-start
        // gates (queue consumer + capacity pump); the enforcer, under the housekeeping leader,
        // finalizes flagged runs whose durable deadline elapsed. Ticket terminalization is
        // shared with the queued-cancel endpoint path.
        services.RemoveAll<IRunCancelStateReader>();
        services.AddSingleton<IRunCancelStateReader, DbRunCancelStateReader>();
        services.AddSingleton<Services.Lifecycle.CancelledTicketFinalizer>();
        services.AddSingleton<Services.Lifecycle.CancelEnforcer>();

        // The persistent FIFO capacity queue replaces the no-op default (DispatcherExtensions);
        // UNIQUE(Project,TicketId) makes "one entry, one queued run row per ticket" a guarantee.
        services.AddScoped<QueuedTicketRepository>();
        services.RemoveAll<ICapacityQueue>();
        services.AddSingleton<ICapacityQueue, DbCapacityQueue>();
        services.AddSingleton<CapacityQueuePumpHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<CapacityQueuePumpHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp => sp.GetRequiredService<CapacityQueuePumpHostedService>().Health);

        // p0336: the DB-backed capacity budget replaces the no-op — the app-owned
        // reservation ledger that makes admission predictable (full footprint
        // reserved before start; released on terminal / delete). Fail-open when no
        // CapacityBudget is configured; the k8s ResourceQuota stays the backstop.
        services.AddScoped<RunCapacityRepository>();
        services.RemoveAll<ICapacityBudget>();
        services.AddSingleton<ICapacityBudget, DbCapacityBudget>();

        // p0327: durable dialogue. Checkpoints + the answer inbox are relational
        // (SpecDialogSession precedent — Redis is a channel, never the authority); the
        // transport decorator writes answers durable-first; the resume sweeper (housekeeping
        // leader) turns answered/expired checkpoints into capacity-queue resume entries.
        // p0393a: the work-spec pointer (repo, sha, hand-back counters) tells a re-trigger's own
        // revision from a reviewer's edit; 2026-09-17-0e79a: beside it, what a person APPROVED.
        services.AddScoped<TicketSeriesRepository>().AddScoped<ApprovedSeriesRepository>();
        services.RemoveAll<ISpecSetPointerStore>().RemoveAll<ISpecApprovalStore>();
        services.AddSingleton<ISpecSetPointerStore, DbSpecSetPointerStore>();
        services.AddSingleton<ISpecApprovalStore, DbSpecApprovalStore>();
        services.AddTicketFactStores(); // c1a7 + 2026-09-25-b4d9
        services.AddScoped<Services.Lifecycle.NotImplementableRetryService>();
        services.AddScoped<RunCheckpointRepository>().AddScoped<DialogueAnswerRepository>();
        services.RemoveAll<IRunCheckpointStore>();
        services.AddSingleton<IRunCheckpointStore, DbRunCheckpointStore>();
        services.RemoveAll<IDialogueAnswerInbox>();
        services.AddSingleton<IDialogueAnswerInbox, DbDialogueAnswerInbox>();
        // p0356: same-ticket resume — the DB-backed prior-run ledger reader
        // replaces the DB-free null default.
        services.RemoveAll<IPriorRunLedgerReader>();
        services.AddSingleton<IPriorRunLedgerReader, DbPriorRunLedgerReader>().AddSingleton<IPreviousAttemptReader, DbPreviousAttemptReader>();
        Decorate<IDialogueTransport>(services, (inner, sp) =>
            new Services.Dialogue.DurableDialogueTransport(
                inner, sp.GetRequiredService<IDialogueAnswerInbox>()));
        services.AddSingleton<Services.ResumeRunLauncher>();
        // p0461: the ticket end of a parked run — an answer written on the work item, and
        // the status move back once the run picks up.
        services.AddSingleton<IParkedTicketDialogue, Services.Lifecycle.ParkedTicketDialogue>();
        // a508: a park that holds no answerable question is reported, not waited out.
        services.AddScoped<ParkedRunRepository>();
        services.AddSingleton<Services.Lifecycle.UnanswerableParkReporter>();
        services.AddSingleton<Services.Lifecycle.DialogueResumeSweeper>();

        // p0246c: the server-side event projector + read store + retention, resolved
        // optionally by CompositeRunEventFanout (Program.cs). p0403: the applier's
        // projections are services it owns, not statics it calls.
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton<RunDbProjector>();
        services.AddRunTracing();
        // p0378: cold-start terminal repair — a RunFinished sitting in the stream
        // beyond the tail-anchored drain cursor is persisted from the stream itself.
        services.AddSingleton<IRunTerminalReconciler, RunTerminalReconciler>();
        services.AddScoped<RunRepository>();
        // p0337: RunDeletionRepository clears the run and its non-cascading satellites in
        // one transaction; RunDeleter force-clears a live run (p0330 kill + lease release +
        // queue removal) before the record delete.
        services.AddScoped<RunDeletionRepository>();
        services.AddScoped<Services.Lifecycle.RunDeleter>();
        // The Criteria met read surface and the operator judgements it applies.
        services.AddScoped<CriteriaMetRepository>().AddScoped<CriterionJudgementRepository>();
        services.AddScoped<RunRetentionService>();

        // p0246e: the run's markdown lives in the DB; 2026-10-02-5ab2f: nothing else holds it.
        services.RemoveAll<IRunArtifactStore>();
        services.AddSingleton<IRunArtifactStore, DbRunArtifactStore>();

        // p0246: migrations are applied EXPLICITLY by `agentsmith database migrate`
        // in the deployment pipeline — NEVER on app startup (replica races + operator
        // surprise). The server assumes the schema is already current.
        services.AddRunLiveness(); // the lease and run-row reapers (2026-10-02-5f89e)
        services.AddHostedService<RunRetentionHostedService>();
        // p0376: keep the UI trail live — drain partial trail buffers on a short timer
        // so a sparse or paused run's events don't sit dark until the batch threshold.
        services.AddHostedService<Services.Events.RunTrailFlusherHostedService>();
        return services.AddAccessPersistence();
    }

    // Generic decoration: wrap whatever implementation of TService was already
    // registered (last-wins) with a decorator, without re-stating the inner
    // registration. Guarded so a minimal graph with no inner registration no-ops.
    private static void Decorate<TService>(
        IServiceCollection services, Func<TService, IServiceProvider, TService> wrap)
        where TService : class
    {
        var existing = services.LastOrDefault(d => d.ServiceType == typeof(TService));
        if (existing is null) return;
        services.Remove(existing);
        services.AddSingleton(sp => wrap((TService)ResolveImpl(existing, sp), sp));
    }

    private static object ResolveImpl(ServiceDescriptor existing, IServiceProvider sp)
    {
        if (existing.ImplementationInstance is { } instance) return instance;
        if (existing.ImplementationFactory is { } factory) return factory(sp);
        return ActivatorUtilities.CreateInstance(sp, existing.ImplementationType!);
    }

    private static PersistenceOptions OptionsFrom(BootstrapConfig config) => new()
    {
        Provider = ProviderOf(config),
        ConnectionString = config.Persistence.ConnectionString,
    };

    private static PersistenceProvider ProviderOf(BootstrapConfig config) =>
        Enum.TryParse<PersistenceProvider>(config.Persistence.Provider, ignoreCase: true, out var p)
            ? p : PersistenceProvider.Sqlite;

    private static IUniqueViolationTranslator TranslatorFor(PersistenceProvider provider) => provider switch
    {
        PersistenceProvider.Sqlite => new SqliteUniqueViolationTranslator(),
        PersistenceProvider.Postgresql => new NpgsqlUniqueViolationTranslator(),
        PersistenceProvider.Mysql => new MySqlUniqueViolationTranslator(),
        PersistenceProvider.SqlServer => new SqlServerUniqueViolationTranslator(),
        _ => new SqliteUniqueViolationTranslator(),
    };
}
