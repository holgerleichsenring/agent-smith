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
///   Progress / Dialogue: null transports (test ergonomics)
///
/// Sandbox stays real — InProcessSandbox by default for the fast tier.
/// Docker-tier callers replace ISandboxFactory after Build() returns.
///
/// Diverging this from production is the bug class this phase prevents;
/// every boundary swap is named and motivated.
/// </summary>
public sealed class RealCompositionHarness : IAsyncDisposable
{
    public IServiceProvider Services { get; }
    public ScriptedChatClient ChatClient { get; }

    private RealCompositionHarness(IServiceProvider services, ScriptedChatClient chatClient)
    {
        Services = services;
        ChatClient = chatClient;
    }

    public static RealCompositionHarness Build(
        string configPath, Action<IServiceCollection>? overrides = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        ServerCompositionBuilder.ConfigureServices(services, configPath);
        ReplaceProductionBoundaries(services, out var chatClient);
        overrides?.Invoke(services);

        return new RealCompositionHarness(services.BuildServiceProvider(), chatClient);
    }

    private static void ReplaceProductionBoundaries(
        IServiceCollection services, out ScriptedChatClient chatClient)
    {
        chatClient = new ScriptedChatClient();
        services.RemoveAll<IChatClientFactory>();
        services.AddSingleton<IChatClientFactory>(new ScriptedChatClientFactoryAdapter(chatClient));

        services.RemoveAll<ISourceProviderFactory>();
        services.AddSingleton<ISourceProviderFactory>(new StubSourceProviderFactory());
        services.RemoveAll<ITicketProviderFactory>();
        services.AddSingleton<ITicketProviderFactory>(new StubTicketProviderFactory());

        services.RemoveAll<IDialogueTransport>();
        services.AddSingleton<IDialogueTransport>(Mock.Of<IDialogueTransport>());
    }

    public AgentSmithConfig Config => Services.GetRequiredService<AgentSmithConfig>();

    public ValueTask DisposeAsync() =>
        Services is IAsyncDisposable disposable ? disposable.DisposeAsync() : ValueTask.CompletedTask;
}
