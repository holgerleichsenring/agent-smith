using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.PipelineHarness.Llm;
using AgentSmith.Server.Services;
using AgentSmith.Tests.TestHelpers;
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

    private RealCompositionHarness(
        IServiceProvider services, ScriptedChatClient chatClient,
        StubSandboxFactory? stubFactory,
        ExtraBindsSandboxFactory? dockerFactory,
        DockerHarnessSession? session)
    {
        Services = services;
        ChatClient = chatClient;
        StubSandboxFactory = stubFactory;
        DockerSandboxFactory = dockerFactory;
        Session = session;
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
        SkillsBackend skillsBackend, Action<IServiceCollection>? overrides = null)
    {
        if (backend == SandboxBackend.Docker && session is null)
            throw new ArgumentNullException(nameof(session),
                "Docker backend requires a DockerHarnessSession (bare repo + working copy).");

        var services = new ServiceCollection();
        ConfigureLogging(services, backend);

        ServerCompositionBuilder.ConfigureServices(services, configPath);
        ReplaceProductionBoundaries(services, skillsBackend, out var chatClient, out var stubFactory);
        if (backend == SandboxBackend.Docker)
            DockerHarnessRegistrations.Apply(services, session!);
        overrides?.Invoke(services);

        var provider = services.BuildServiceProvider();
        var dockerFactory = backend == SandboxBackend.Docker
            ? (ExtraBindsSandboxFactory)provider.GetRequiredService<ISandboxFactory>()
            : null;
        return new RealCompositionHarness(provider, chatClient,
            backend == SandboxBackend.Stub ? stubFactory : null,
            dockerFactory, session);
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
        out ScriptedChatClient chatClient, out StubSandboxFactory sandboxFactory)
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

    public AgentSmithConfig Config => Services.GetRequiredService<AgentSmithConfig>();

    public ValueTask DisposeAsync() =>
        Services is IAsyncDisposable disposable ? disposable.DisposeAsync() : ValueTask.CompletedTask;
}
