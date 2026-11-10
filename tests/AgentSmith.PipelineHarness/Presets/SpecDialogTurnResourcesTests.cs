using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
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
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-10-4778 review: a design turn gives back what it holds — its read scopes and its work clones — on every
/// exit, through the REAL server composition: when the model call fails and when the turn is cancelled mid-call.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogTurnResourcesTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";

    private readonly List<Mock<ISourceScopeSandbox>> _scopes = [];

    [Fact]
    public async Task DialogTurn_ModelCallFails_TurnResourcesAreDisposed()
    {
        await using var harness = BuildHarness();
        harness.ChatClient.EnqueueThrow(new InvalidOperationException("the model is gone"));

        await RunTurnAsync(harness, CancellationToken.None);

        _scopes.Should().NotBeEmpty();
        _scopes.ForEach(s => s.Verify(x => x.DisposeAsync(), Times.Once));
    }

    [Fact]
    public async Task DialogTurn_CancelledMidCall_TurnResourcesAreDisposed()
    {
        await using var harness = BuildHarness();
        using var cts = new CancellationTokenSource();
        harness.ChatClient.EnqueueDeferred(() =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await RunTurnAsync(harness, cts.Token);

        _scopes.Should().NotBeEmpty();
        _scopes.ForEach(s => s.Verify(x => x.DisposeAsync(), Times.Once));
    }

    private RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            var factory = new Mock<ISourceScopeSandboxFactory>();
            factory.Setup(f => f.Create(It.IsAny<ResolvedProject>(), It.IsAny<RepoConnection>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .Returns((ResolvedProject _, RepoConnection repo, string? _, string? _) =>
                {
                    var scope = new Mock<ISourceScopeSandbox>();
                    scope.SetupGet(s => s.RepoName).Returns(repo.Name);
                    scope.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);
                    _scopes.Add(scope);
                    return scope.Object;
                });
            services.RemoveAll<ISourceScopeSandboxFactory>();
            services.AddSingleton(factory.Object);
        });

    private static async Task RunTurnAsync(RealCompositionHarness harness, CancellationToken ct)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var opened = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .OpenAsync(DispatcherDefaults.PlatformDashboard, "d-resources", "d-resources", "U-resources",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        var state = opened.AppendTurn(new TranscriptTurn(TranscriptRole.User, "what does the router do?", DateTimeOffset.UtcNow));
        try
        {
            await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
                .RunTurnAsync(state, new TurnPoster("U1", true), ct);
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
        {
            // Whether the turn answers with a failure note or lets the exception through, it gives back what it held.
        }
    }
}
