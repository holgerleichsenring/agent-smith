using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.PipelineHarness.Llm;
using AgentSmith.PipelineHarness.Presets;
using AgentSmith.Server.Services;
using AgentSmith.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199: builds the SAME ServiceProvider Server.Program.cs builds via
/// ServerCompositionBuilder, then overrides the agreed boundaries:
///   LLM (IChatClientFactory): scripted, the only real mock
///   Ticket / Source providers: fixture-backed (documented trade-off)
///   ISandboxFactory: StubSandboxFactory by default; SandboxBackend.Docker
///     swaps the production DockerSandboxFactory + a per-test source
///     provider that pushes/clones a host-side bare git repo (p0199b).
///   IPromptCatalog / ISwaggerProvider: stubbed (skill-catalog + swagger
///     fetch are external data, not handler logic).
///   INucleiScanner / ISpectralScanner / IZapScanner: empty-findings stubs
///     by default; the env-gate AGENTSMITH_HARNESS_REAL_SCANNERS=1 keeps
///     the production adapters wired so an operator can run the heavy
///     scanner images against StubApiTargetHost (p0199f).
///   Progress / Dialogue: null transports (test ergonomics).
///
/// Diverging this from production is the bug class this phase prevents;
/// every boundary swap is named and motivated.
/// </summary>
public sealed class RealCompositionHarness : IAsyncDisposable
{
    public IServiceProvider Services { get; }
    public ScriptedChatClient ChatClient { get; }
    internal StubSandboxFactory? StubSandboxFactory { get; }
    public ExtraBindsSandboxFactory? DockerSandboxFactory { get; }
    public DockerHarnessSession? Session { get; }
    private readonly string _configDbPath;

    private RealCompositionHarness(
        IServiceProvider services, ScriptedChatClient chatClient,
        StubSandboxFactory? stubFactory,
        ExtraBindsSandboxFactory? dockerFactory,
        DockerHarnessSession? session,
        string configDbPath)
    {
        Services = services;
        ChatClient = chatClient;
        StubSandboxFactory = stubFactory;
        DockerSandboxFactory = dockerFactory;
        Session = session;
        _configDbPath = configDbPath;
    }

    // Back-compat for the 18 fast-tier tests that don't pass a backend.
    public static RealCompositionHarness Build(
        string configPath, Action<IServiceCollection>? overrides = null)
        => Build(configPath, SandboxBackend.Stub, session: null,
            SkillsBackend.Stub, overrides);

    public static RealCompositionHarness Build(
        string configPath, SandboxBackend backend, DockerHarnessSession? session,
        Action<IServiceCollection>? overrides = null)
        => Build(configPath, backend, session, SkillsBackend.Stub, overrides);

    public static RealCompositionHarness Build(
        string configPath, SandboxBackend backend, DockerHarnessSession? session,
        SkillsBackend skillsBackend, Action<IServiceCollection>? overrides = null,
        bool? realScanners = null)
    {
        if (backend == SandboxBackend.Docker && session is null)
            throw new ArgumentNullException(nameof(session),
                "Docker backend requires a DockerHarnessSession (bare repo + working copy).");

        var services = new ServiceCollection();
        ConfigureLogging(services, backend);

        ServerCompositionBuilder.ConfigureServices(services, configPath);
        // p0349: the server now loads its config from the DB. Point the bootstrap at
        // a throwaway sqlite config DB (instead of the unopenable default file path)
        // and seed the fixture config into it after build, so the harness mirrors
        // production's DB-backed config path.
        var configDbPath = Path.Combine(
            Path.GetTempPath(), $"agentsmith-harness-config-{Guid.NewGuid():N}.db");
        RegisterConfigBootstrap(services, configPath, configDbPath);
        ReplaceProductionBoundaries(services, skillsBackend, out var chatClient, out var stubFactory, realScanners);
        if (backend == SandboxBackend.Docker)
        {
            RestoreRealConnectionMultiplexer(services);
            DockerHarnessRegistrations.Apply(services, session!);
        }
        overrides?.Invoke(services);

        var provider = services.BuildServiceProvider();
        SeedConfigStore(provider, configPath);
        var dockerFactory = backend == SandboxBackend.Docker
            ? (ExtraBindsSandboxFactory)provider.GetRequiredService<ISandboxFactory>()
            : null;
        return new RealCompositionHarness(provider, chatClient,
            backend == SandboxBackend.Stub ? stubFactory : null,
            dockerFactory, session, configDbPath);
    }

    // p0349: bootstrap the DbContext connection from a per-harness sqlite file (the
    // DB the server loads config from cannot use the unopenable default path in CI),
    // carrying the fixture's secret names so ${...} refs resolve as before.
    private static void RegisterConfigBootstrap(
        IServiceCollection services, string configPath, string configDbPath)
    {
        var raw = RawConfigYaml.Deserialize(File.ReadAllText(configPath));
        services.RemoveAll<BootstrapConfig>();
        services.AddSingleton(new BootstrapConfig(
            new PersistenceConfig { Provider = "sqlite", ConnectionString = $"Data Source={configDbPath}" },
            raw.Secrets));
    }

    // p0349: migrate the throwaway config DB and import the fixture config into it —
    // the same guarded import path production uses for the one-shot ConfigMap cutover
    // — so DbConfigurationLoader assembles the fixture's agents/projects/registries.
    private static void SeedConfigStore(IServiceProvider provider, string configPath)
    {
        using (var scope = provider.CreateScope())
            scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>().Database.Migrate();
        var docStore = provider.GetRequiredService<IConfigDocumentStore>();
        // Idempotent: durable-dialogue "restart" tests re-enter Build over the SAME
        // shared DB — the config is already seeded, so importing again would be a
        // guarded "store not empty" reject. Skip when already configured.
        if (!docStore.IsEmpty()) return;
        var raw = RawConfigYaml.Deserialize(File.ReadAllText(configPath));
        var writes = provider.GetRequiredService<ConfigDocumentAssembler>().Decompose(raw)
            .Select(d => new ConfigDocWrite(d.Type, d.Id, d.Doc, null, d.Edges, "harness"))
            .ToList();
        docStore.Import(writes, force: false);
    }

    private static void ConfigureLogging(IServiceCollection services, SandboxBackend backend) =>
        services.AddLogging(b =>
        {
            var verbose = backend == SandboxBackend.Docker
                || string.Equals(
                    Environment.GetEnvironmentVariable("AGENTSMITH_HARNESS_VERBOSE"),
                    "1", StringComparison.Ordinal);
            if (verbose)
            {
                b.AddSimpleConsole(o => { o.SingleLine = true; });
                b.SetMinimumLevel(LogLevel.Information);
            }
            else
            {
                b.AddProvider(NullLoggerProvider.Instance);
            }
        });

    private static void ReplaceProductionBoundaries(
        IServiceCollection services, SkillsBackend skillsBackend,
        out ScriptedChatClient chatClient, out StubSandboxFactory sandboxFactory,
        bool? realScanners = null)
    {
        chatClient = new ScriptedChatClient();
        services.RemoveAll<IChatClientFactory>();
        services.AddSingleton<IChatClientFactory>(new ScriptedChatClientFactoryAdapter(chatClient));

        services.RemoveAll<ISourceProviderFactory>();
        services.AddSingleton<ISourceProviderFactory>(new StubSourceProviderFactory());
        services.RemoveAll<ITicketProviderFactory>();
        services.AddSingleton<ITicketProviderFactory>(new StubTicketProviderFactory());

        sandboxFactory = new StubSandboxFactory();
        services.RemoveAll<ISandboxFactory>();
        services.AddSingleton<ISandboxFactory>(sandboxFactory);

        services.RemoveAll<IPromptCatalog>();
        services.AddSingleton<IPromptCatalog, StubPromptCatalog>();
        services.RemoveAll<ISwaggerProvider>();
        services.AddSingleton<ISwaggerProvider, StubSwaggerProvider>();

        // p0199f: api-scan scanner adapters are stubbed by default — the
        // real Nuclei / Spectral / ZAP images are heavy and per-operator.
        // AGENTSMITH_HARNESS_REAL_SCANNERS=1 keeps the production adapters
        // in place; tests that opt in own the cost (and the docker daemon).
        // p0343b: tests opt in via the realScanners PARAMETER, never by
        // mutating the process env — xUnit runs collections in parallel, and
        // a process-wide env toggle raced every concurrently-composing fast
        // preset into real scanners ("Spectral ruleset not found" flake).
        // The env remains as the operator/CLI opt-in only.
        if (!(realScanners ?? RealScannersOptedIn())) ApiScannerStubs.Register(services);

        // Skill-catalog resolution touches the network (default source) or
        // a checked-out git tree (local source). Stub mode points Root at an
        // empty temp dir — fast-tier tests asserting only handler shape
        // downstream of LoadSkills are happy with an empty catalog. Fixture
        // mode (p0199d) points Root at the checked-in SkillsCatalog tree so
        // YamlSkillLoader walks real role definitions; init-project and
        // autonomous need that to populate AvailableRoles.
        services.RemoveAll<ISkillsCatalogPath>();
        if (skillsBackend == SkillsBackend.Fixture)
            services.AddSingleton<ISkillsCatalogPath, CheckedInSkillsCatalogPath>();
        else
            services.AddSingleton<ISkillsCatalogPath, FixtureSkillsCatalogPath>();

        services.RemoveAll<IDialogueTransport>();
        services.AddSingleton<IDialogueTransport>(Mock.Of<IDialogueTransport>());

        ReplaceRedisBackedServices(services);
    }

    // Fast tier must run with no Redis available (CI doesn't have it).
    // ServerCompositionBuilder.AddRedis wires up the live connection +
    // every Redis-backed singleton. Swap the multiplexer to a mock and
    // swap the three handler-visible services (event publishers + run
    // artifact store) to their existing no-op / in-memory variants. The
    // remaining Redis-backed singletons (queue, claim-lock, leader-lease,
    // heartbeat, conversation lookup, project-map store) stay registered
    // — they aren't resolved by any handler the harness exercises today,
    // and resolving them lazily would only fail if the test starts using
    // them, which is the right place to extend this swap.
    private static void ReplaceRedisBackedServices(IServiceCollection services)
    {
        services.RemoveAll<StackExchange.Redis.IConnectionMultiplexer>();
        services.AddSingleton(Mock.Of<StackExchange.Redis.IConnectionMultiplexer>());

        services.RemoveAll<AgentSmith.Contracts.Events.IEventPublisher>();
        services.AddSingleton<AgentSmith.Contracts.Events.IEventPublisher,
            AgentSmith.Application.Services.Events.NoOpEventPublisher>();

        services.RemoveAll<AgentSmith.Contracts.Events.ISystemEventPublisher>();
        services.AddSingleton<AgentSmith.Contracts.Events.ISystemEventPublisher,
            AgentSmith.Application.Services.Events.NoOpSystemEventPublisher>();

        services.RemoveAll<AgentSmith.Contracts.Persistence.IRunArtifactStore>();
        services.AddSingleton<AgentSmith.Contracts.Persistence.IRunArtifactStore,
            AgentSmith.Application.Services.Persistence.InMemoryRunArtifactStore>();
    }

    // p0199d: docker-tier needs a REAL IConnectionMultiplexer so the
    // production DockerSandbox + SandboxRedisChannel can push step
    // descriptors to the in-container agent. ReplaceRedisBackedServices
    // mocks it for fast tier (where no Redis is available); docker tier
    // pre-validates host Redis reachability and then restores the real
    // connection via REDIS_URL (or localhost:6379).
    private static void RestoreRealConnectionMultiplexer(IServiceCollection services)
    {
        var url = Environment.GetEnvironmentVariable("REDIS_URL") ?? "localhost:6379";
        services.RemoveAll<StackExchange.Redis.IConnectionMultiplexer>();
        services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
            StackExchange.Redis.ConnectionMultiplexer.Connect(url + ",abortConnect=false"));
    }

    public AgentSmithConfig Config => Services.GetRequiredService<AgentSmithConfig>();

    public const string RealScannersEnv = "AGENTSMITH_HARNESS_REAL_SCANNERS";

    public static bool RealScannersOptedIn() =>
        string.Equals(
            Environment.GetEnvironmentVariable(RealScannersEnv),
            "1", StringComparison.Ordinal);

    public async ValueTask DisposeAsync()
    {
        if (Services is IAsyncDisposable disposable) await disposable.DisposeAsync();
        foreach (var f in new[] { _configDbPath, _configDbPath + "-wal", _configDbPath + "-shm" })
            if (File.Exists(f)) try { File.Delete(f); } catch (IOException) { /* best-effort temp cleanup */ }
    }
}
