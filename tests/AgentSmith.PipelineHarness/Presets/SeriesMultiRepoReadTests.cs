using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.Specs.MultiRepo;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-f5f7b fast-tier: the series reader as the server composes it. Two hand-placed
/// sub-series — one in Server, one in Client, stamped by one write — are read as ONE set in id
/// order, with the last commit of each repository.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesMultiRepoReadTests
{
    [Fact]
    public async Task TwoSubSeries_ServerAndClient_ReadAsOneSetInIdOrder()
    {
        var branches = new SeriesRepoBranches();
        string[] both = ["Server", "Client"];
        branches.SubSeries("Server", SeriesRepoBranches.T2, both, [SeriesRepoBranches.Id('a'), SeriesRepoBranches.Id('c')]);
        branches.SubSeries("Client", SeriesRepoBranches.T2, both, [SeriesRepoBranches.Id('b')]);
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISandboxFileReaderFactory>();
            services.AddSingleton(branches.Files());
        });

        var result = await branches.ReadWithAsync(
            harness.Services.GetRequiredService<ISpecSetReader>(), "Server", both);

        result.State.Should().Be(SpecSetBranchState.Answered);
        result.Set!.Specs.Select(p => p.SpecId).Should().Equal(
            SeriesRepoBranches.Id('a'), SeriesRepoBranches.Id('b'), SeriesRepoBranches.Id('c'));
        result.Read!.RevisionShas.Keys.Should().BeEquivalentTo(both);
        result.Read.Lagging.Should().BeEmpty();
    }
}
