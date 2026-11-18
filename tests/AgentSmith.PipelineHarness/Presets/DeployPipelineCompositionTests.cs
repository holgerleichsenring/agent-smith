using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Deployment;
using AgentSmith.Tests.Deployment;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-18-3863e, real server composition: a template that delivers by pipeline_trigger — the launcher queues the
/// 'deploy' run and the run, through the composed deliveries, starts the pipeline on the repository's host with the
/// version as an input, and tells the server the run to follow (2026-11-18-3863f). The pipeline runner is the harness's
/// boundary, as the source host is.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DeployPipelineCompositionTests
{
    private const string Project = "fixture-deploy";

    [Fact]
    public async Task Deploy_ToTest_StartsThePipelineWithTheVersion()
    {
        var fixture = new DeployFixture();
        fixture.Template.Files[DeployFixture.DeclarationPath] = DeployPipelineTriggerTests.Declaration;
        fixture.Runner.Setup(r => r.StartAsync(It.IsAny<PipelineRunOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineRunStart("42", "https://ci.example/runs/42"));
        fixture.Runner.Setup(r => r.ReadAsync("42", It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineRunState(CiStates.Pending, "https://ci.example/runs/42", []));
        var queued = new List<PipelineRequest>();
        var published = new List<RunEvent>();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Deploy), services =>
        {
            DeployCompositionTests.Boundaries(services, fixture);
            var runners = new Mock<IPipelineRunnerFactory>();
            runners.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == "app"))).Returns(fixture.Runner.Object);
            services.RemoveAll<IPipelineRunnerFactory>().AddSingleton(runners.Object);
            var queue = new Mock<IRedisJobQueue>();
            queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
                .Callback<PipelineRequest, CancellationToken>((r, _) => queued.Add(r)).Returns(Task.CompletedTask);
            services.RemoveAll<IRedisJobQueue>().AddSingleton(queue.Object);
            var events = new Mock<IEventPublisher>(); // 2026-11-18-3863f: what the server is told to follow
            events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
                .Callback<RunEvent, CancellationToken>((e, _) => published.Add(e)).Returns(Task.CompletedTask);
            services.RemoveAll<IEventPublisher>().AddSingleton(events.Object);
        });
        await using var scope = harness.Services.CreateAsyncScope();

        var launch = await scope.ServiceProvider.GetRequiredService<DeployLauncher>()
            .LaunchAsync(Project, new DeployLaunchRequest("api", "test", "v2"), default);
        launch.Outcome.Should().Be(DeployLaunchOutcome.Started, launch.Reason);
        var result = await RunAsync(harness, JsonSerializer.Deserialize<PipelineRequest>(JsonSerializer.Serialize(queued.Single()))!);

        result.IsSuccess.Should().BeTrue(result.Message);
        fixture.Runner.Verify(r => r.StartAsync(It.Is<PipelineRunOrder>(o => o.Pipeline == "deploy.yml" && o.Inputs["version"] == "v2"),
            It.IsAny<CancellationToken>()), Times.Once);
        var delivered = published.OfType<DeployDeliveredEvent>().Single();
        delivered.PipelineRunId.Should().Be("42");
        delivered.Repo.Should().Be("app");
        delivered.Url.Should().Be("https://ci.example/runs/42");
        delivered.Environment.Should().Be("test"); // the harness builds its project in code, without a name
    }

    private static async Task<Domain.Models.CommandResult> RunAsync(RealCompositionHarness harness, PipelineRequest request)
    {
        var config = harness.Services.GetRequiredService<IConfigurationLoader>()
            .LoadConfig(harness.Services.GetRequiredService<ServerContext>().ConfigPath);
        var runner = new PipelineRunner(harness.Services)
        {
            RepoOverride = DeployFixture.AppRepo, TemplatesOverride = config.Projects[Project].Templates,
        };
        foreach (var (key, value) in request.Context!) runner.RequestContext[key] = value;
        runner.RequestContext[ContextKeys.RunId] = request.RunId!; // the run id the server queued it under
        return await runner.RunAsync(PipelinePresets.DeployName);
    }
}
