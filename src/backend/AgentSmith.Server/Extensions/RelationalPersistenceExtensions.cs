using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
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
        services.AddSingleton<IConfigStore, DbConfigStore>();
        services.AddScoped<ActiveRunRepository>();
        services.AddScoped<RunArtifactRepository>();
        // p0315a: spec-dialog sessions are DB-authoritative (volatile Redis must
        // never be the only holder of a design transcript).
        services.AddScoped<SpecDialogSessionRepository>();

        services.RemoveAll<IActiveRunLease>();
        services.AddSingleton<IActiveRunLease, DbActiveRunLease>();
        services.AddSingleton<ActiveRunReaper>();

        // p0330: cancel is persistent state — the DB-backed flag reader feeds the
        // pre-start gates (queue consumer + capacity pump), and the enforcer
        // (hosted under the housekeeping leader) force-kills flagged runs whose
        // durable deadline elapsed. Ticket terminalization is shared with the
        // queued-cancel endpoint path.
        services.RemoveAll<IRunCancelStateReader>();
        services.AddSingleton<IRunCancelStateReader, DbRunCancelStateReader>();
        services.AddSingleton<Services.Lifecycle.CancelledTicketFinalizer>();
        services.AddSingleton<Services.Lifecycle.CancelEnforcer>();

        // p0320c: the persistent FIFO capacity queue + its dequeue pump. The
        // no-op default (DispatcherExtensions) is replaced by the DB-backed
        // queue whose UNIQUE(Project,TicketId) makes "one entry, one queued
        // run row per ticket" a database guarantee.
        services.AddScoped<QueuedTicketRepository>();
        services.RemoveAll<ICapacityQueue>();
        services.AddSingleton<ICapacityQueue, DbCapacityQueue>();
        services.AddHostedService<CapacityQueuePumpHostedService>();

        // p0336: the DB-backed capacity budget replaces the no-op — the app-owned
        // reservation ledger that makes admission predictable (full footprint
        // reserved before start; released on terminal / delete). Fail-open when no
        // CapacityBudget is configured; the k8s ResourceQuota stays the backstop.
        services.AddScoped<RunCapacityRepository>();
        services.RemoveAll<ICapacityBudget>();
        services.AddSingleton<ICapacityBudget, DbCapacityBudget>();

        // p0327: durable dialogue. Checkpoints + the answer inbox are relational
        // (SpecDialogSession precedent — Redis is a channel, never the authority);
        // the transport decorator writes answers durable-first; the resume
        // sweeper (housekeeping leader) turns answered/expired checkpoints into
        // capacity-queue resume entries the pump launches.
        services.AddScoped<RunCheckpointRepository>();
        services.AddScoped<DialogueAnswerRepository>();
        services.RemoveAll<IRunCheckpointStore>();
        services.AddSingleton<IRunCheckpointStore, DbRunCheckpointStore>();
        services.RemoveAll<IDialogueAnswerInbox>();
        services.AddSingleton<IDialogueAnswerInbox, DbDialogueAnswerInbox>();
        // p0356: same-ticket resume — the DB-backed prior-run ledger reader
        // replaces the DB-free null default.
        services.RemoveAll<IPriorRunLedgerReader>();
        services.AddSingleton<IPriorRunLedgerReader, DbPriorRunLedgerReader>();
        Decorate<IDialogueTransport>(services, (inner, sp) =>
            new Services.Dialogue.DurableDialogueTransport(
                inner, sp.GetRequiredService<IDialogueAnswerInbox>()));
        services.AddSingleton<Services.ResumeRunLauncher>();
        services.AddSingleton<Services.Lifecycle.DialogueResumeSweeper>();

        // p0246c: the server-side event projector + read store + retention. The
        // projector is resolved optionally by CompositeRunEventFanout (Program.cs).
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton<RunDbProjector>();
        services.AddScoped<RunRepository>();
        // p0337: single-run + bulk-terminal delete. RunDeletionRepository clears
        // the run and its non-cascading satellites in one transaction; RunDeleter
        // force-clears a live run (p0330 kill + lease release + queue removal)
        // before the record delete.
        services.AddScoped<RunDeletionRepository>();
        services.AddScoped<Services.Lifecycle.RunDeleter>();
        // p0329: ratification outcomes → expectation-metrics read surface.
        services.AddScoped<ExpectationMetricsRepository>();
        services.AddScoped<RunRetentionService>();

        // p0262: the ticket-lifecycle status is no longer stored or read as authority —
        // it is DERIVED from the native ticket status + the ActiveRun lease. The
        // p0246d DB-authoritative transitioner decorator is gone; transitions write the
        // platform label directly as a pure marker (unconditional, no DB).

        // p0246e: mirror the durable markdown slots into the DB so result.md /
        // plan.md survive a process restart AND a Redis flush.
        Decorate<IRunArtifactStore>(services, (inner, sp) =>
            new DbRunArtifactStore(inner, sp.GetRequiredService<IServiceScopeFactory>()));

        // p0246: migrations are applied EXPLICITLY by `agentsmith database migrate`
        // in the deployment pipeline — NEVER on app startup (replica races + operator
        // surprise). The server assumes the schema is already current.
        services.AddHostedService<ActiveRunReaperHostedService>();
        services.AddHostedService<RunRetentionHostedService>();
        return services;
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
