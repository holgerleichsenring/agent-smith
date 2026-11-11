using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Events;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-10-d854c: a design turn is its run. Through the REAL server composition, the run id reaches the
/// conversation's row while the turn runs, and a turn that throws or is cancelled leaves a Notice turn naming it.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogTurnRunTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";
    private const string Dialog = "d-turn-run";

    [Fact]
    public async Task Runner_ThrownTurn_AppendsANoticeTurnWithItsRunId()
    {
        // No step guard catches the run's own end: the event store failing on RunFinished throws out of the pipeline,
        // after the grounding step named the run.
        await using var harness = BuildHarness(ThrowingOnRunFinished.Install);
        harness.ChatClient.EnqueueText("Dispatch flows through the intent engine.");
        var state = await OpenAsync(harness, Project);

        var result = await RunTurnAsync(harness, state, CancellationToken.None);

        result.Should().BeNull("a thrown turn has kept and sent its notice already");
        var notice = (await TranscriptAsync(harness)).Last();
        notice.Kind.Should().Be(SpecDialogTurnKind.Notice);
        notice.Text.Should().Contain(ThrowingOnRunFinished.Message);
        notice.RunId.Should().NotBeNullOrEmpty("the pipeline had started, so the turn names its run");
        (await RunningRunIdAsync(harness)).Should().Be(notice.RunId, "the row named the run as it was grounded");
    }

    [Fact]
    public async Task Runner_ThrowBeforeThePipelineStarts_AppendsANoticeWithNoRunId()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness, Project) with { Project = "no-such-project" };

        var result = await RunTurnAsync(harness, state, CancellationToken.None);

        result.Should().BeNull();
        var notice = (await TranscriptAsync(harness)).Last();
        notice.Kind.Should().Be(SpecDialogTurnKind.Notice);
        notice.RunId.Should().BeNull("no run was started");
    }

    [Fact]
    public async Task Runner_CancelledTurn_AppendsANoticeWithCancellationTokenNone()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness, Project);
        using var cts = new CancellationTokenSource();
        harness.ChatClient.EnqueueDeferred(() =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        var act = () => RunTurnAsync(harness, state, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        var notice = (await TranscriptAsync(harness)).Last();
        notice.Text.Should().Be(SpecDialogTurnEnding.CancelledNotice, "written on CancellationToken.None, past the cancel");
        notice.RunId.Should().NotBeNullOrEmpty();
    }

    private static RealCompositionHarness BuildHarness(Action<IServiceCollection>? more = null) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            more?.Invoke(services);
        });

    /// <summary>The harness's event publisher, failing on the run's end — after the run was named on the row.</summary>
    private sealed class ThrowingOnRunFinished(IEventPublisher inner) : IEventPublisher
    {
        public const string Message = "the event store went away";

        public static void Install(IServiceCollection services)
        {
            var registered = services.Last(d => d.ServiceType == typeof(IEventPublisher));
            services.Remove(registered);
            services.AddSingleton<IEventPublisher>(provider => new ThrowingOnRunFinished(Inner(registered, provider)));
        }

        public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
            runEvent is RunFinishedEvent ? throw new InvalidOperationException(Message) : inner.PublishAsync(runEvent, cancellationToken);

        private static IEventPublisher Inner(ServiceDescriptor registered, IServiceProvider provider) =>
            (IEventPublisher)(registered.ImplementationInstance
                ?? registered.ImplementationFactory?.Invoke(provider)
                ?? ActivatorUtilities.CreateInstance(provider, registered.ImplementationType!));
    }

    private static async Task<ConversationState> OpenAsync(RealCompositionHarness harness, string project)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>();
        await sessions.OpenAsync(DispatcherDefaults.PlatformDashboard, Dialog, Dialog, "U-run",
            new ActiveScope { Project = project, Repos = [Repo] }, CancellationToken.None);
        return (await sessions.AppendTurnAsync(DispatcherDefaults.PlatformDashboard, Dialog, TranscriptRole.User,
            "cut the ordering feature into slices", null, null, CancellationToken.None))!;
    }

    private static async Task<SpecDialogTurnResult?> RunTurnAsync(
        RealCompositionHarness harness, ConversationState state, CancellationToken ct)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, new TurnPoster("U1", false), ct);
    }

    private static async Task<IReadOnlyList<TranscriptTurn>> TranscriptAsync(RealCompositionHarness harness) =>
        (await StateAsync(harness)).Transcript;

    private static async Task<string?> RunningRunIdAsync(RealCompositionHarness harness) =>
        (await StateAsync(harness)).RunningRunId;

    private static async Task<ConversationState> StateAsync(RealCompositionHarness harness)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .GetOpenByThreadAsync(DispatcherDefaults.PlatformDashboard, Dialog, CancellationToken.None))!;
    }
}
