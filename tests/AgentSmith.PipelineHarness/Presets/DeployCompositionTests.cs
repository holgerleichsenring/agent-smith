using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Deployment;
using AgentSmith.Tests.Deployment;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Specs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-18-3863c, real server composition: a person's deploy is accepted by the endpoint with a run id, and the
/// 'deploy' preset — built from the launcher's own seeds — delivers test's manifest change and merges it. The template
/// scope, its reader and the source host are the harness's boundary.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DeployCompositionTests
{
    private const string Project = "fixture-deploy";

    [Fact]
    public async Task Deploy_ToTest_IsAcceptedAndItsRunMergesTheManifestChange()
    {
        var fixture = new DeployFixture();
        var queued = new List<PipelineRequest>();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Deploy), services =>
        {
            Boundaries(services, fixture);
            var queue = new Mock<IRedisJobQueue>();
            queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
                .Callback<PipelineRequest, CancellationToken>((r, _) => queued.Add(r)).Returns(Task.CompletedTask);
            services.RemoveAll<IRedisJobQueue>().AddSingleton(queue.Object);
        });
        await using var scope = harness.Services.CreateAsyncScope();

        var accepted = await DeploymentEndpoints.DeployAsync(Project,
            new DeployLaunchRequest("api", "test", "v2", TicketId: "7"), scope.ServiceProvider.GetRequiredService<DeployLauncher>(), default);
        accepted.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status200OK);

        var config = harness.Services.GetRequiredService<IConfigurationLoader>()
            .LoadConfig(harness.Services.GetRequiredService<ServerContext>().ConfigPath);
        var runner = new PipelineRunner(harness.Services)
        {
            RepoOverride = DeployFixture.AppRepo, TemplatesOverride = config.Projects[Project].Templates,
        };
        // The request as the queue hands it back: serialized, so every seed is a string the builder reads.
        var request = JsonSerializer.Deserialize<PipelineRequest>(JsonSerializer.Serialize(queued.Single()))!;
        request.PipelineName.Should().Be(PipelinePresets.DeployName);
        request.ShownOnTicket!.Value.Should().Be("7", "the run is listed on the ticket it was started from");
        foreach (var (key, value) in request.Context!) runner.RequestContext[key] = value;
        var result = await runner.RunAsync(PipelinePresets.DeployName);

        result.IsSuccess.Should().BeTrue(result.Message);
        fixture.App.Verify(p => p.MergePullRequestAsync("https://stub.test/app/pull/1", It.IsAny<BranchName>(),
            It.IsAny<BranchProtection>(), It.IsAny<CancellationToken>()), Times.Once, "test needs no approval");
        fixture.App.Verify(p => p.WriteFilesToBranchAsync(It.IsAny<BranchName>(),
            It.Is<IReadOnlyList<RepoFile>>(f => f.Single().Content == DeployFixture.Manifest("v2")),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deploy_UnboundEnvironment_IsRefusedBeforeARunExists()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Deploy), services => services.AddDashboardApi());
        await using var scope = harness.Services.CreateAsyncScope();

        var refused = await scope.ServiceProvider.GetRequiredService<DeployLauncher>()
            .LaunchAsync(Project, new DeployLaunchRequest("api", "staging"), default);

        refused.Outcome.Should().Be(DeployLaunchOutcome.Refused);
        refused.Reason.Should().Contain("binds no environment 'staging'");
    }

    private static void Boundaries(IServiceCollection services, DeployFixture fixture)
    {
        services.AddDashboardApi(); // the dashboard's deploy act, as Program composes it
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == "app"))).Returns(fixture.App.Object);
        sources.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == "gitops"))).Returns(fixture.Ops.Object);
        services.RemoveAll<ISourceProviderFactory>().AddSingleton(sources.Object);
        services.RemoveAll<ISourceScopeSandboxFactory>().AddSingleton<ISourceScopeSandboxFactory>(
            new CodingMasterTemplateTests.FixedScopeFactory(new CodingMasterTemplateTests.RecordingScope()));
        services.RemoveAll<ISandboxFileReaderFactory>().AddSingleton<ISandboxFileReaderFactory>(
            new DerivationTestLooks.FixedReaderFactory(fixture.Template));
    }
}
