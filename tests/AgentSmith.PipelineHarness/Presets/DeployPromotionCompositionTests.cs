using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Deployment;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-18-3863h, real server composition: a test deploy that settled green deploys preview (promotes from it, no
/// approval) once and leaves prod (approval) for a person; the series master's deploy goes through the same launcher
/// and refuses an approval environment. The job queue is the boundary: what it receives is what would run.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DeployPromotionCompositionTests
{
    private const string Project = "fixture-deploy";
    private readonly List<PipelineRequest> _queued = [];

    [Fact]
    public async Task Promotion_GreenMergedTestDeploy_StartsPreviewOnceAndLeavesProdForAPerson()
    {
        await using var harness = Harness();
        var project = harness.Services.GetRequiredService<IConfigurationLoader>()
            .LoadConfig(harness.Services.GetRequiredService<ServerContext>().ConfigPath).Projects[Project];
        await using (var scope = harness.Services.CreateAsyncScope())
            await new DeployRecordProjection().ApplyAsync(scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), new DeployDeliveredEvent(
                "r-src", DateTimeOffset.UtcNow, Project, "api", null, "test", "v2", "gitops_pr", "app", "https://stub.test/app/pull/1",
                "https://stub.test/app/pull/1", null, ["preview", "prod"]), default);

        var note = await PromoteSettledAsync(harness, project);
        await PromoteSettledAsync(harness, project);

        var promoted = _queued.Should().ContainSingle("a settled deploy promotes once").Subject;
        promoted.PipelineName.Should().Be(PipelinePresets.DeployName);
        promoted.Context![ContextKeys.DeployEnvironment].Should().Be("preview");
        promoted.Context[ContextKeys.DeployPromotedVersion].Should().Be("v2");
        promoted.Context.Should().NotContainKey(ContextKeys.DeployVersion, "a promotion is not a typed version");
        note.Should().Contain("promoted v2 to 'preview'").And.Contain("'prod' not promoted");
    }

    [Fact]
    public async Task SeriesDeploy_ApprovalEnvironmentIsRefused_ANoApprovalOneQueuesListedOnItsTicket()
    {
        await using var harness = Harness();
        await using var scope = harness.Services.CreateAsyncScope();
        var acts = scope.ServiceProvider.GetRequiredService<ISeriesTicketActs>();

        var refused = await acts.DeployAsync(Project, "api", "prod", null, null, default);
        var started = await acts.DeployAsync(Project, "api", "test", "v3", "7", default);

        refused.Should().Contain("needs a person's approval");
        started.Should().StartWith("Deploy of api to test started");
        _queued.Should().ContainSingle().Which.ShownOnTicket!.Value.Should().Be("7");
    }

    private async Task<string?> PromoteSettledAsync(RealCompositionHarness harness, ResolvedProject project)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var record = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<Infrastructure.Persistence.Entities.DeployRecord>()
            .FindAsync("r-src");
        if (record!.SettledAtTicks is null)
            (record.PullRequestState, record.CiState, record.SettledAtTicks) = (PrReviewStates.Merged, CiStates.Green, DateTimeOffset.UtcNow.UtcTicks);
        await scope.ServiceProvider.GetRequiredService<DeployPromotion>().PromoteAsync(record, project, default);
        await scope.ServiceProvider.GetRequiredService<DeployRecordRepository>().SaveAsync(default);
        return record.Note;
    }

    private RealCompositionHarness Harness() => RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Deploy), services =>
    {
        services.AddDashboardApi();
        var queue = new Mock<IRedisJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRequest, CancellationToken>((r, _) => _queued.Add(r)).Returns(Task.CompletedTask);
        services.RemoveAll<IRedisJobQueue>().AddSingleton(queue.Object);
    });
}
