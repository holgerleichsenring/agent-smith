using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Services.Events;

/// <summary>
/// 2026-10-15-4a4dc: the live hub's run-to-project index — a known project costs nothing, a live run
/// picked up mid-stream reads its row once, and every other reader reads through and leaves no entry.
/// </summary>
public sealed class RunProjectIndexTests : IDisposable
{
    private readonly HubProjectStore _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task RunProjectIndex_KnownProject_ReadsNothing()
    {
        var index = _store.Index();

        (await index.OfAsync("r-known", "alpha", CancellationToken.None)).Should().Be("alpha");
        (await index.LiveAsync("r-known", "alpha", CancellationToken.None)).Should().Be("alpha");

        _store.Reads.Should().Be(0);
    }

    [Fact]
    public async Task RunProjectIndex_LiveSnapshotWithoutProject_ReadsTheRowOnceAndRemembersIt()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-live", "alpha"));
        var index = _store.Index();

        (await index.LiveAsync("r-live", null, CancellationToken.None)).Should().Be("alpha");
        (await index.LiveAsync("r-live", null, CancellationToken.None)).Should().Be("alpha");
        (await index.OfAsync("r-live", null, CancellationToken.None)).Should().Be("alpha");

        _store.Reads.Should().Be(1, "a live run's project is read once and remembered until it finishes");
    }

    [Fact]
    public async Task RunProjectIndex_OfAsync_ReadsThroughAndRemembersNothing()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-done", "alpha"));
        var index = _store.Index();

        (await index.OfAsync("r-done", null, CancellationToken.None)).Should().Be("alpha");
        (await index.OfAsync("r-done", null, CancellationToken.None)).Should().Be("alpha");

        _store.Reads.Should().Be(2, "a finished run asked over the hub is never remembered");
    }

    [Fact]
    public async Task RunProjectIndex_Forget_DropsTheEntry()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-forget", "alpha"));
        var index = _store.Index();
        await index.LiveAsync("r-forget", null, CancellationToken.None);

        index.Forget("r-forget");
        await index.OfAsync("r-forget", null, CancellationToken.None);

        _store.Reads.Should().Be(2, "a forgotten run is read again");
    }

    [Fact]
    public async Task RunProjectIndex_NoRowOrEmptyProject_IsNull()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-empty", ""));
        var index = _store.Index();
        var withoutAStore = new RunProjectIndex(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        (await index.OfAsync("r-missing", null, CancellationToken.None)).Should().BeNull();
        (await index.LiveAsync("r-empty", null, CancellationToken.None)).Should().BeNull();
        (await withoutAStore.OfAsync("r-any", null, CancellationToken.None)).Should().BeNull();
    }
}
