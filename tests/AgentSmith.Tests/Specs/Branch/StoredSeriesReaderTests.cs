using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs.Branch;

/// <summary>2026-11-28-db88a: a run's set is its row's — answered with the row's shas and executed ids, a series repository
/// outside the checkout named, no row nothing, a stored value that does not read back unreadable.</summary>
public sealed class StoredSeriesReaderTests
{
    private const string Key = "azdo-4711";
    private static readonly RepoConnection Server = new() { Name = "Server" };

    [Fact]
    public async Task ReadAsync_NoRow_IsNothing()
    {
        var read = await Reader(new InMemorySpecSetPointerStore()).ReadAsync(Pipeline(), Server, [Server], new TicketKey(Key), default);

        read.State.Should().Be(SpecSetBranchState.NothingAtThePath);
    }

    [Fact]
    public async Task ReadAsync_AStoredSet_AnswersWithTheRowsShasAndExecutedIds()
    {
        var store = new InMemorySpecSetPointerStore();
        await store.SaveSetAsync("p", Set() with { SeriesRepos = ["Server"] }, default);
        await store.SaveAsync("p", new SpecSetPointer(Key, "Server", "sha-1", 1,
            RevisionShas: new Dictionary<string, string> { ["Server"] = "sha-1" }, ExecutedIds: ["2026-11-28-0a0aa"]), default);

        var read = await Reader(store).ReadAsync(Pipeline(), Server, [Server], new TicketKey(Key), default);

        read.State.Should().Be(SpecSetBranchState.Answered);
        read.Read!.LastCommitSha.Should().Be("sha-1");
        read.Read.RevisionShas.Should().Equal(new Dictionary<string, string> { ["Server"] = "sha-1" });
        read.Set!.Executed.Should().Equal("2026-11-28-0a0aa");
        read.Set.Source.Should().Be(SpecSource.BranchArtifact);
    }

    [Fact]
    public async Task ReadAsync_ASeriesRepositoryNotCheckedOut_IsNamed()
    {
        var store = new InMemorySpecSetPointerStore();
        await store.SaveSetAsync("p", Set() with { SeriesRepos = ["Server", "Client"] }, default);

        var read = await Reader(store).ReadAsync(Pipeline(), Server, [Server], new TicketKey(Key), default);

        read.State.Should().Be(SpecSetBranchState.MemberNotCheckedOut);
        read.Why.Should().Contain("series member 'Client' is not checked out in this run");
    }

    [Fact]
    public async Task ReadAsync_AStoredValueThatDoesNotReadBack_IsUnreadableNotReplaced()
    {
        var store = new Mock<ISpecSetPointerStore>();
        store.Setup(s => s.GetAsync("p", Key, It.IsAny<CancellationToken>())).ReturnsAsync(new SpecSetPointer(Key, "Server", "sha", 1));
        store.Setup(s => s.GetSetAsync("p", Key, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("The spec set stored for azdo-4711 in project p does not read back"));

        var read = await Reader(store.Object).ReadAsync(Pipeline(), Server, [Server], new TicketKey(Key), default);

        read.State.Should().Be(SpecSetBranchState.Unreadable);
        store.Verify(s => s.SaveSetAsync(It.IsAny<string>(), It.IsAny<SpecSet>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
    }

    private static StoredSeriesReader Reader(ISpecSetPointerStore store) =>
        new(store, SeriesDoubles.Reader(SeriesDoubles.EmptyFiles()), new SandboxTargets(), NullLogger<StoredSeriesReader>.Instance);

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, "p");
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["Server"] = Mock.Of<ISandbox>() });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, new Dictionary<string, string> { ["Server"] = "Server" });
        return pipeline;
    }

    private static SpecSet Set() =>
        new(Key, [new DerivedSpec(new Contracts.Models.SpecDraft("2026-11-28-0a0aa", "g", "spec: 2026-11-28-0a0aa", []) { Repos = ["Server"] },
                "a", string.Empty, [])],
            SpecAccounting.Empty, [new SpecRevision(1, "initial", DateTimeOffset.UnixEpoch)], SpecSource.Approved, Series: "2026-11-28-0a0a");
}
