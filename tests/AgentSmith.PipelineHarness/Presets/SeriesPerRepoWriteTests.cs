using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.Specs.MultiRepo;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-f5f7d fast-tier: the publish and the record as the server composes them, in the shape
/// that put a series into the wrong repository — a scope of BackgroundWorker and Server, one spec that
/// changes Server. The sub-series, the done record and the index line all land in Server; the worker
/// gets nothing.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesPerRepoWriteTests
{
    private static readonly RepoConnection Worker = new() { Name = "BackgroundWorker", Type = RepoType.Local };
    private static readonly RepoConnection Server = new() { Name = "Server", Type = RepoType.Local };

    [Fact]
    public async Task ScopeWorkerAndServer_SpecForServer_EverythingLandsInServerOnly()
    {
        var branches = new SeriesRepoBranches();
        branches.Repo(Worker.Name);
        branches.Repo(Server.Name);
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISandboxFileReaderFactory>();
            services.AddSingleton(branches.Files());
        });
        var pipeline = Pipeline(branches);
        var set = Tests.Specs.MultiRepo.SeriesPerRepoWriteTests.Set(("a", "Server"));

        var published = await harness.Services.GetRequiredService<ISpecSetPublisher>().PublishAsync(
            pipeline, "p", new SeriesScope([Worker, Server], null, [], null), set, [], default);
        pipeline.Set(ContextKeys.CurrentSpec, set.Specs[0].Draft);
        var recorded = await harness.Services.GetRequiredService<ICommandHandler<WriteSpecRecordContext>>().ExecuteAsync(
            new WriteSpecRecordContext(pipeline.Get<Domain.Entities.Repository>(ContextKeys.Repository), pipeline,
                [Worker, Server]), default);

        published.IsSuccess.Should().BeTrue(published.Message);
        recorded.IsSuccess.Should().BeTrue(recorded.Message);
        branches.Repo(Worker.Name).Paths.Should().BeEmpty("the spec changes nothing in the worker");
        branches.Repo(Server.Name).Paths.Should().Contain([
            SeriesPaths.Manifest(SeriesRepoBranches.Base),
            $"{SeriesPaths.Done}/{SeriesRepoBranches.Id('a')}-a.yaml",
            "/work/.agentsmith/contexts/default/context.yaml",
        ]);
        pipeline.Get<string>(ContextKeys.SpecRepo).Should().Be("Server");
    }

    private static PipelineContext Pipeline(SeriesRepoBranches branches)
    {
        var pipeline = branches.Pipeline();
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>
            {
                [Worker.Name] = new("default", ".", "C#"), [Server.Name] = new("default", ".", "C#"),
            });
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set(ContextKeys.ProjectName, "p");
        return pipeline;
    }
}
