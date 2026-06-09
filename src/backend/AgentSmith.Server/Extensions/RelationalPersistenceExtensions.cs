using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Services;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Services.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddDbContext<AgentSmithDbContext>(
            (sp, b) => b.UseProvider(OptionsFrom(sp.GetRequiredService<AgentSmithConfig>())),
            ServiceLifetime.Scoped);
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AgentSmithDbContext>());
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IUniqueViolationTranslator>(sp =>
            TranslatorFor(ProviderOf(sp.GetRequiredService<AgentSmithConfig>())));
        services.AddScoped<ActiveRunRepository>();
        services.AddScoped<RunArtifactRepository>();
        services.AddScoped<TicketLifecycleRepository>();

        services.RemoveAll<IActiveRunLease>();
        services.AddSingleton<IActiveRunLease, DbActiveRunLease>();
        services.AddSingleton<ActiveRunReaper>();

        // p0246c: the server-side event projector + read store + retention. The
        // projector is resolved optionally by CompositeRunEventFanout (Program.cs).
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton<RunDbProjector>();
        services.AddScoped<RunRepository>();
        services.AddScoped<RunRetentionService>();

        // p0246d: the DB becomes the ticket-lifecycle system-of-record. Decorate
        // the existing transitioner factory so every transition writes the
        // authoritative DB status first + the platform label best-effort.
        Decorate<ITicketStatusTransitionerFactory>(services, (inner, sp) =>
            new DbAuthoritativeTransitionerFactory(
                inner, sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<ILoggerFactory>()));

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

    private static PersistenceOptions OptionsFrom(AgentSmithConfig config) => new()
    {
        Provider = ProviderOf(config),
        ConnectionString = config.Persistence.ConnectionString,
    };

    private static PersistenceProvider ProviderOf(AgentSmithConfig config) =>
        Enum.TryParse<PersistenceProvider>(config.Persistence.Provider, ignoreCase: true, out var p)
            ? p : PersistenceProvider.Sqlite;

    private static IUniqueViolationTranslator TranslatorFor(PersistenceProvider provider) => provider switch
    {
        PersistenceProvider.Sqlite => new SqliteUniqueViolationTranslator(),
        PersistenceProvider.Postgresql => new NpgsqlUniqueViolationTranslator(),
        PersistenceProvider.Mysql => new MySqlUniqueViolationTranslator(),
        _ => new SqliteUniqueViolationTranslator(),
    };
}
