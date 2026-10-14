using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-13-8762m: the record tools a master start gets, built in one place, and the memory index
/// reader the spec-execution start and LoadMemoryIndex share — both resolved from the real container.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class MasterRecordToolsTests
{
    [Fact]
    public async Task MasterRecordToolHosts_Create_BuildsLogRecallRememberPerRepository()
    {
        var decisions = new MasterHandlerFixture.NoOpDecisionLogger();
        var pipeline = TwoRepositories();

        var records = MasterHandlerFixture.RecordHosts(decisions).Create(pipeline, "/work");

        records.Decisions.Should().BeSameAs(decisions);
        records.HandForward.Should().BeNull("a master without a current spec hands nothing forward");
        (await records.Remember.Remember("project", "bus", "the bus", "body"))
            .Should().Contain("this run has 2 repositories").And.Contain("[shop, portal]");
        (await records.Log.LogDecision("Architecture", "one bus")).Should().Contain("[shop, portal]");
        records.Recall.GetTools(null, null).Should().ContainSingle();

        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft("2026-10-14-5678a", "goal", "spec: 2026-10-14-5678a\n", []));
        MasterHandlerFixture.RecordHosts(decisions).Create(pipeline, "/work").HandForward
            .Should().NotBeNull().And.Subject.As<HandForwardToolHost>().GetTools(null, null).Single().Name
            .Should().Be(HandForwardToolHost.Name);
    }

    [Fact]
    public void MemoryIndexReader_ResolvesThroughTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<MemoryIndexReader>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<MasterRecordToolHosts>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ICommandHandler<AgentSmith.Application.Models.LoadMemoryIndexContext>>()
            .Should().BeOfType<LoadMemoryIndexHandler>();
        scope.ServiceProvider.GetRequiredService<ICommandHandler<AgentSmith.Application.Models.AgenticMasterContext>>()
            .Should().BeOfType<AgenticMasterHandler>();
    }

    [Fact]
    public async Task MasterRecordToolHosts_DefaultDecision_ResolvedThroughTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        var events = new AgentSmith.Tests.TestHelpers.RecordingEventPublisher();
        services.AddSingleton<AgentSmith.Contracts.Events.IEventPublisher>(events);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var pipeline = TwoRepositories();
        pipeline.Set(ContextKeys.RunId, "2026-10-14T1100-di");

        var records = scope.ServiceProvider.GetRequiredService<MasterRecordToolHosts>().Create(pipeline, "/work");
        var answer = await records.DefaultDecision!.DecideByDefault("q", "c", ["d"], "w");

        answer.Should().StartWith("Taken by default");
        events.Events.OfType<AgentSmith.Contracts.Events.DefaultDecisionTakenEvent>().Should().ContainSingle()
            .Which.RunId.Should().Be("2026-10-14T1100-di");
    }

    private static PipelineContext TwoRepositories()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox>
        {
            ["shop-dotnet"] = Mock.Of<ISandbox>(), ["portal-node"] = Mock.Of<ISandbox>(),
        });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string> { ["shop-dotnet"] = "shop", ["portal-node"] = "portal" });
        return pipeline;
    }
}
