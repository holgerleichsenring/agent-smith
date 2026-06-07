using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Services;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Services.Hosting;
using AgentSmith.Server.Services.Sandbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0246b: opt-in relational persistence. When a provider is configured (env
/// AGENTSMITH_PERSISTENCE_PROVIDER, e.g. sqlite|postgresql|mysql), swaps the
/// no-op single-run lease for the DB-backed DbActiveRunLease (whose
/// UNIQUE(Project,TicketId) index becomes the authoritative guard) and starts the
/// positive-evidence reaper. Unset → no-op, so the default Server keeps the
/// Redis-only claim path and no DB dependency. A richer agentsmith.yml
/// `persistence:` block lands with the projector (p0246c).
/// </summary>
internal static class RelationalPersistenceExtensions
{
    public const string ProviderEnv = "AGENTSMITH_PERSISTENCE_PROVIDER";
    public const string ConnectionEnv = "AGENTSMITH_PERSISTENCE_CONNECTION";

    internal static IServiceCollection AddRelationalPersistence(this IServiceCollection services)
    {
        var options = ReadOptions();
        if (options is null) return services; // persistence off → keep the NoOp lease

        services.AddDbContextFactory<AgentSmithDbContext>(b => b.UseProvider(options));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IUniqueViolationTranslator>(TranslatorFor(options.Provider));

        services.RemoveAll<IActiveRunLease>();
        services.AddSingleton<IActiveRunLease, DbActiveRunLease>();
        services.AddSingleton<IRunLivenessProbe, OrchestratorRunLivenessProbe>();
        services.AddSingleton<ActiveRunReaper>();

        // p0246c: the server-side event projector + read store + retention. The
        // projector is resolved optionally by CompositeRunEventFanout (Program.cs).
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton<RunDbProjector>();
        services.AddSingleton<DbRunStore>();
        services.AddSingleton<RunRetentionService>();

        // p0246d: the DB becomes the ticket-lifecycle system-of-record. Decorate
        // the existing transitioner factory so every transition writes the
        // authoritative DB status first + the platform label best-effort.
        services.AddSingleton<DbTicketLifecycleStore>();
        Decorate<ITicketStatusTransitionerFactory>(services, (inner, sp) =>
            new DbAuthoritativeTransitionerFactory(
                inner, sp.GetRequiredService<DbTicketLifecycleStore>(), sp.GetRequiredService<ILoggerFactory>()));

        // p0246e: mirror the durable markdown slots into the DB so result.md /
        // plan.md survive a process restart AND a Redis flush.
        Decorate<IRunArtifactStore>(services, (inner, sp) =>
            new DbRunArtifactStore(inner, sp.GetRequiredService<IDbContextFactory<AgentSmithDbContext>>()));

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

    private static PersistenceOptions? ReadOptions()
    {
        var provider = Environment.GetEnvironmentVariable(ProviderEnv);
        if (string.IsNullOrWhiteSpace(provider)) return null;
        if (!Enum.TryParse<PersistenceProvider>(provider, ignoreCase: true, out var parsed)) return null;
        var connection = Environment.GetEnvironmentVariable(ConnectionEnv);
        return new PersistenceOptions
        {
            Provider = parsed,
            ConnectionString = string.IsNullOrWhiteSpace(connection)
                ? new PersistenceOptions().ConnectionString : connection,
        };
    }

    private static IUniqueViolationTranslator TranslatorFor(PersistenceProvider provider) => provider switch
    {
        PersistenceProvider.Sqlite => new SqliteUniqueViolationTranslator(),
        PersistenceProvider.Postgresql => new NpgsqlUniqueViolationTranslator(),
        PersistenceProvider.Mysql => new MySqlUniqueViolationTranslator(),
        _ => new SqliteUniqueViolationTranslator(),
    };
}
