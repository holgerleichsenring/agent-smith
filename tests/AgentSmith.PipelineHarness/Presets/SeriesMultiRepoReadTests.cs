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
/// <para>2026-11-28-db88a: only a ticket in flight at that deploy is read this way — its row stores no set — and the read is
/// imported onto the row once; SpecSetSourceCompositionTests pins the multi-repository read of a stored set.</para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesMultiRepoReadTests
{
    [Fact]
    public async Task TwoSubSeries_ARowInFlight_ReadAsOneSetInIdOrderAndImportedOnce()
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

        var store = harness.Services.GetRequiredService<ISpecSetPointerStore>();
        await store.SaveAsync(string.Empty, new SpecSetPointer(SeriesRepoBranches.Key, "Server", "sha-Server", 1,
            RevisionShas: new Dictionary<string, string> { ["Server"] = "sha-Server", ["Client"] = "sha-Client" }), default);

        var result = await branches.ReadWithAsync(
            harness.Services.GetRequiredService<ISpecSetReader>(), "Server", both);

        result.State.Should().Be(SpecSetBranchState.Answered);
        result.Set!.Specs.Select(p => p.SpecId).Should().Equal(
            SeriesRepoBranches.Id('a'), SeriesRepoBranches.Id('b'), SeriesRepoBranches.Id('c'));
        result.Read!.RevisionShas.Keys.Should().BeEquivalentTo(both);
        result.Read.Lagging.Should().BeEmpty();
        (await store.GetSetAsync(string.Empty, SeriesRepoBranches.Key, default))!.Specs.Should().HaveCount(3, "imported once");
    }
}
