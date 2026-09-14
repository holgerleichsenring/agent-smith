using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Access;
using AgentSmith.Server.Services.Hosting;
using AgentSmith.Tests.Server.Auth;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-08-26-7a51: the access surface over the real DB stack — a migrated in-memory
/// SQLite, the document store WITH the admin invariant wrapped around it, the observed
/// callers, and the role source reading the store the way the server does.
/// <para>
/// The environment admin grant is STATED rather than set: it reaches the invariant through
/// a captured delegate, so a test says what is configured instead of mutating the process
/// every other test in the suite shares.
/// </para>
/// </summary>
internal sealed class AccessTestHarness : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public AccessTestHarness(string? adminGrant = null, TokenAuthorityConfig? auth = null)
    {
        Auth = auth ?? new TokenAuthorityConfig();
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var services = new ServiceCollection();
        services.AddDbContext<AgentSmithDbContext>(b => b.UseSqlite(_connection), ServiceLifetime.Scoped);
        services.AddScoped<ConfigDocumentRepository>();
        services.AddScoped<ConfigImportRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AgentSmithDbContext>());
        services.AddScoped<ObservedCallerRepository>();
        services.AddSingleton<ConfigDocumentAssembler>();
        services.AddSingleton<ConfigDocJson>();
        services.AddSingleton<RawConfigYaml>();
        services.AddSingleton<ConfigYamlExporter>();
        services.AddSingleton<EfConfigDocumentStore>();
        services.AddSingleton(new AdminRoute(ResolverUnderTest.Grant(adminGrant)));
        services.AddSingleton<IConfigDocumentStore>(sp => new AdminReachableConfigDocumentStore(
            sp.GetRequiredService<EfConfigDocumentStore>(),
            sp.GetRequiredService<AdminRoute>(), sp.GetRequiredService<ConfigDocJson>()));
        services.AddSingleton<IConfigStore, DbConfigStore>();
        services.AddSingleton<IObservedCallerStore, EfObservedCallerStore>();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>().Database.Migrate();

        Mapping = new RoleMappingSource(
            new StoredRoleMapping(Store, NullLogger<StoredRoleMapping>.Instance), Auth);
        Mapping.AdoptStore();
        Writer = new AccessGrantWriter(
            Store, Mapping, new CustomRoleRules(), new RoleRemovalGuard(Observed), Json);
        // 2026-09-14-2b7c: ONE buffer, shared by the removal and by whatever observes — a
        // test whose remover and resolver held different buffers could not see the defect
        // this phase exists for, because the note that suppresses a return is in-memory.
        Buffer = new CallerObservationBuffer(TimeProvider.System);
        Remover = new PersonRemover(Mapping, Observed, Buffer, Writer);
        Reader = new AccessSurfaceReader(
            Mapping, Observed, new AccessViewComposer(new AccessPeopleComposer()),
            NullLogger<AccessSurfaceReader>.Instance);
    }

    public TokenAuthorityConfig Auth { get; }
    public IConfigStore Store => _provider.GetRequiredService<IConfigStore>();
    public IConfigDocumentStore DocStore => _provider.GetRequiredService<IConfigDocumentStore>();

    /// <summary>
    /// The store WITHOUT the invariant — how a row an installation upgraded with got there,
    /// which is the only way a revert can have a routeless document to revert to.
    /// </summary>
    public EfConfigDocumentStore RawDocStore => _provider.GetRequiredService<EfConfigDocumentStore>();
    public IObservedCallerStore Observed => _provider.GetRequiredService<IObservedCallerStore>();
    public ConfigDocJson Json => _provider.GetRequiredService<ConfigDocJson>();
    public ConfigDocumentAssembler Assembler => _provider.GetRequiredService<ConfigDocumentAssembler>();
    public RoleMappingSource Mapping { get; }
    public AccessGrantWriter Writer { get; }
    public PersonRemover Remover { get; }
    public AccessSurfaceReader Reader { get; }

    /// <summary>2026-09-14-2b7c: the one buffer the removal and the resolver share.</summary>
    public CallerObservationBuffer Buffer { get; }

    /// <summary>
    /// What the flush service does, on demand — the drain and the write are one operation to a
    /// removal, and a test that drained without the gate would be testing a shape that does not
    /// ship.
    /// </summary>
    public Task FlushAsync(CancellationToken ct = default) =>
        new CallerObservationFlushHostedService(
            Observed, Buffer, NullLogger<CallerObservationFlushHostedService>.Instance)
            .FlushAsync(ct);

    /// <summary>A resolver over this harness's mapping — what the authorization path asks.</summary>
    public CallerIdentityResolver Resolver(
        string? adminGrant = null, AgentSmith.Server.Contracts.ICallerObservations? observations = null) =>
        ResolverUnderTest.Resolver(Mapping, ResolverUnderTest.Grant(adminGrant), observations);

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }
}
