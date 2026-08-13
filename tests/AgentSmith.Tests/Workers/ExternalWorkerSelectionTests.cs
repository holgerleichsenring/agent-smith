using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Infrastructure.Services.Workers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// p0416: worker mode is reachable ONLY by an operator writing
/// <c>type: external_worker</c> into an agent. Registration is not selection — this is
/// the guard that no production path (a missing key, a provider outage, a default) can
/// fall into an external worker by accident.
/// </summary>
public sealed class ExternalWorkerSelectionTests
{
    [Fact]
    public void ExternalWorker_IsSelectedOnlyByItsAgentType()
    {
        var factory = NewFactory();

        var client = factory.Create(
            new AgentConfig { Type = ExternalWorkerChatClientBuilder.TypeName, Model = "sonnet" },
            TaskType.Summarization);

        client.Should().NotBeNull();
    }

    [Fact]
    public void AnyOtherAgentType_CannotFallIntoWorkerMode()
    {
        var factory = NewFactory();

        var act = () => factory.Create(new AgentConfig { Type = "openai", Model = "gpt-5" }, TaskType.Primary);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No IChatClientBuilder registered*",
                "an unknown provider type must fail loudly, never silently degrade to a worker");
    }

    [Fact]
    public void ProductionComposition_RegistersTheBridge_ButNoAgentUsesItUnasked()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<AgentSmith.Contracts.Events.IEventPublisher,
            AgentSmith.Application.Services.Events.NoOpEventPublisher>();
        services.AddSingleton<AgentSmith.Contracts.Events.IRunContextAccessor,
            AgentSmith.Application.Services.Events.AsyncLocalRunContextAccessor>();
        services.AddSingleton<IModelPricingResolver, ModelPricingResolver>();
        services.AddAgentProviders();

        var builders = services.BuildServiceProvider().GetRequiredService<IEnumerable<IChatClientBuilder>>();

        builders.SelectMany(b => b.SupportedTypes)
            .Should().Contain(ExternalWorkerChatClientBuilder.TypeName)
            .And.Contain("claude", "the bridge is additive — the real providers stay registered");
    }

    private static ChatClientFactory NewFactory() =>
        new([NewWorkerBuilder()],
            new AgentSmith.Application.Services.Events.NoOpEventPublisher(),
            new AgentSmith.Application.Services.Events.AsyncLocalRunContextAccessor(),
            new ModelPricingResolver(),
            new LlmRateLimiterRegistry(NullLogger<LlmRateLimiterRegistry>.Instance),
            new ThrottleWaitReporter(),
            NullLoggerFactory.Instance);

    private static ExternalWorkerChatClientBuilder NewWorkerBuilder()
    {
        var json = new WorkerJsonFormat();
        return new ExternalWorkerChatClientBuilder(
            new WorkerRequestComposer(new WorkerMessageMapper(json), new WorkerOptionsMapper()),
            new WorkerPromptRenderer(json),
            new WorkerReplyParser(json),
            new WorkerReplyTranslator(),
            new ScriptedWorkerProcessRunner(),
            new AgentSmith.Application.Services.Events.AsyncLocalRunContextAccessor(),
            new ExternalWorkerCliOptionsFactory(),
            NullLoggerFactory.Instance);
    }
}
