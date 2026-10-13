using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-13-9a59e: the published cut through the composition root's own registration, over a
/// migrated SQLite store — one cut row per publish event, not_started rows for the unexecuted specs,
/// a re-cut prunes what it dropped, a spec's start and verdict land on its row, and the cut dies with
/// its run.
/// </summary>
public sealed class RunSeriesCutProjectionTests : IDisposable
{
    private static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private const string RunId = "2026-10-13T0900-cccc";
    private const string A = "2026-10-13-1234a";
    private const string B = "2026-10-13-1234b";
    private const string C = "2026-10-13-1234c";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;
    private long _seq;

    public RunSeriesCutProjectionTests()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton(Mock.Of<ITicketActivityNudge>());
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SpecCut_Published_CreatesNotStartedRowsForUnexecutedSpecsOnly()
    {
        await ApplyAsync(Published([A], Entry(A, 1), Entry(B, 2), Entry(C, 3)));

        var rows = await SpecsAsync();

        rows.Select(r => (r.SpecId, r.Ordinal, r.Status, r.StartedAt)).Should().Equal(
            (B, 2, "not_started", (DateTimeOffset?)null), (C, 3, "not_started", (DateTimeOffset?)null));
        rows[0].Title.Should().Be($"Goal of {B}");
        (await CutsAsync()).Single().SetKey.Should().Be("github-42");
    }

    [Fact]
    public async Task SpecCut_ReplayedEvent_InsertsOneCutRow()
    {
        var published = Published([], Entry(A, 1));
        await ApplyAsync(published, seq: 4);
        await ApplyAsync(published, seq: 4);

        (await CutsAsync()).Should().ContainSingle();
        (await SpecsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task SpecCut_RecutInRun_PrunesDroppedNotStartedRows()
    {
        await ApplyAsync(Published([], Entry(A, 1), Entry(B, 2), Entry(C, 3)));
        await ApplyAsync(State(A, SpecRunState.InProgress, T.AddMinutes(1)));
        await ApplyAsync(Published([], Entry(A, 1), Entry(C, 2)));

        var rows = await SpecsAsync();

        rows.Select(r => (r.SpecId, r.Status, r.Ordinal)).Should().Equal((A, "in_progress", 1), (C, "not_started", 2));
        (await CutsAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task SpecState_FirstInProgress_SetsStartedAt()
    {
        await ApplyAsync(Published([], Entry(A, 1)));
        await ApplyAsync(State(A, SpecRunState.InProgress, T.AddMinutes(2)));
        await ApplyAsync(State(A, SpecRunState.InProgress, T.AddMinutes(9), acceptance: "{\"criteria\":[]}"));
        await ApplyAsync(State(A, SpecRunState.Done, T.AddMinutes(12)));

        var row = (await SpecsAsync()).Single();

        row.StartedAt.Should().Be(T.AddMinutes(2), "the first start stands");
        row.AcceptanceJson.Should().Be("{\"criteria\":[]}", "an event without a verdict keeps the last one");
        row.Status.Should().Be("done");
    }

    [Fact]
    public async Task RunDeletion_RemovesSeriesCutRows()
    {
        await ApplyAsync(Published([], Entry(A, 1)));

        await using (var ctx = new AgentSmithDbContext(Options()))
            await new RunDeletionRepository(ctx).DeleteAsync(RunId, CancellationToken.None);

        (await CutsAsync()).Should().BeEmpty();
        (await SpecsAsync()).Should().BeEmpty();
    }

    private static SpecCutEntry Entry(string specId, int ordinal) =>
        new(specId, ordinal, $"{specId}-x", $"Goal of {specId}", ["api"], [new SpecStep("s1", "do", null)], ["it builds"], "spec: x");

    private static SpecSetPublishedEvent Published(IReadOnlyList<string> carried, params SpecCutEntry[] specs) =>
        new(RunId, T, 1, "initial", "2026-10-13-1234", carried, Committed: true, SetKey: "github-42", Specs: specs);

    private static SpecStateChangedEvent State(string specId, SpecRunState state, DateTimeOffset at, string? acceptance = null) =>
        new(RunId, specId, 1, $"Goal of {specId}", state, null, at, acceptance);

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(AgentSmith.Contracts.Events.RunEvent ev, long? seq = null)
    {
        using var scope = _provider.CreateScope();
        await _provider.GetRequiredService<RunEventApplier>().ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), ev, seq ?? ++_seq, CancellationToken.None);
    }

    private async Task<List<RunSpec>> SpecsAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<RunSpec>().Where(r => r.RunId == RunId).OrderBy(r => r.Ordinal).ToListAsync();
    }

    private async Task<List<RunSeriesCut>> CutsAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<RunSeriesCut>().Where(r => r.RunId == RunId).ToListAsync();
    }
}
