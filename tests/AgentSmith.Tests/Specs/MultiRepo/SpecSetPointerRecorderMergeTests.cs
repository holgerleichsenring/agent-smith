using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs.MultiRepo;

/// <summary>2026-10-12-f5f7b: the recorder merges the repository it wrote into the sha map.</summary>
public sealed class SpecSetPointerRecorderMergeTests
{
    [Fact]
    public async Task RecordAsync_SecondRepository_MergesItsShaAndKeepsTheFirst()
    {
        var store = new InMemorySpecSetPointerStore();
        var recorder = new SpecSetPointerRecorder(store, NullLogger<SpecSetPointerRecorder>.Instance);

        await recorder.RecordAsync("p", new RepoConnection { Name = "Server" }, Set(), "s1", default);
        await recorder.RecordAsync("p", new RepoConnection { Name = "Client" }, Set(), "c1", default);

        var pointer = await store.GetAsync("p", "azdo-1", default);
        pointer!.ShaByRepo.Should().BeEquivalentTo(new Dictionary<string, string> { ["Server"] = "s1", ["Client"] = "c1" });
        pointer.CarryingRepo.Should().Be("Client", "CarryingRepo is still written");
        pointer.RevisionSha.Should().Be("c1");
    }

    [Fact]
    public async Task RecordAsync_OverALegacyRow_KeepsTheBackFilledCarrier()
    {
        var store = new InMemorySpecSetPointerStore();
        await store.SaveAsync("p", new SpecSetPointer("azdo-1", "Server", "s0", 1), default);

        await new SpecSetPointerRecorder(store, NullLogger<SpecSetPointerRecorder>.Instance)
            .RecordAsync("p", new RepoConnection { Name = "Server" }, Set(), "s1", default);

        (await store.GetAsync("p", "azdo-1", default))!.RevisionShas
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["Server"] = "s1" });
    }

    private static SpecSet Set() => new(
        "azdo-1", [new SpecPhase(new PhaseDraft("x1a", "g", "spec: x1a", []), "x1a", string.Empty, [])],
        SpecAccounting.Empty, [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.BranchArtifact, Series: "x1");
}
