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
///   ISandboxFactory: defaults to StubSandboxFactory for fast tier;
///     docker-tier tests pass an `overrides` callback that re-registers
///     the real DockerSandboxFactory after the auto-detected backend has
///     been wiped.
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
    internal StubSandboxFactory SandboxFactory { get; }

    private RealCompositionHarness(
        IServiceProvider services, ScriptedChatClient chatClient, StubSandboxFactory sandboxFactory)
    {
        Services = services;
        ChatClient = chatClient;
        SandboxFactory = sandboxFactory;
    }

    public static RealCompositionHarness Build(
        string configPath, Action<IServiceCollection>? overrides = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        ServerCompositionBuilder.ConfigureServices(services, configPath);
        ReplaceProductionBoundaries(services, out var chatClient, out var sandboxFactory);
        overrides?.Invoke(services);

        return new RealCompositionHarness(
            services.BuildServiceProvider(), chatClient, sandboxFactory);
    }

    private static void ReplaceProductionBoundaries(
        IServiceCollection services,
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
        // a checked-out git tree (local source). The catalog content is not
        // what this harness asserts on — handler behaviour is. Pre-bind the
        // path to an empty temp dir so consumers see Root without going
        // through SkillsBootstrapHostedService.
        services.RemoveAll<ISkillsCatalogPath>();
        services.AddSingleton<ISkillsCatalogPath, FixtureSkillsCatalogPath>();

        services.RemoveAll<IDialogueTransport>();
        services.AddSingleton<IDialogueTransport>(Mock.Of<IDialogueTransport>());
    }

    public AgentSmithConfig Config => Services.GetRequiredService<AgentSmithConfig>();

    public ValueTask DisposeAsync() =>
        Services is IAsyncDisposable disposable ? disposable.DisposeAsync() : ValueTask.CompletedTask;
}
