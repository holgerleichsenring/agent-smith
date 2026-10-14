using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
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
/// 2026-10-13-9a59d: the attempt record through the composition root's own registration, over a
/// migrated SQLite store — the cause is set once, the revision is the last publish's, the row dies
/// with its run, and a new attempt's start names its ticket.
/// </summary>
public sealed class RunAttemptProjectionTests : IDisposable
{
    private static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private const string RunId = "2026-10-13T0900-aaaa";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly Mock<ITicketActivityNudge> _activity = new();
    private readonly ServiceProvider _provider;
    private long _seq;

    public RunAttemptProjectionTests()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton(_activity.Object);
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(AgentSmith.Contracts.Events.RunEvent ev, long? seq = null)
    {
        using var scope = _provider.CreateScope();
        await _provider.GetRequiredService<RunEventApplier>().ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), ev, seq ?? ++_seq, CancellationToken.None);
    }

    private async Task<List<RunAttempt>> RowsAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<RunAttempt>().AsNoTracking().ToListAsync();
    }

    private static TicketFetchedEvent Fetched(AttemptCause cause, string state = "Ready") =>
        new(RunId, "42", "Widget", "Body", state, [], 0, "GitHub", T, Cause: cause,
            TicketFingerprint: new string('a', 64), WebUrl: "https://github.com/o/api/issues/42");

    private static RunStartedEvent Started(string runId = RunId, string pipeline = "code") =>
        new(runId, "ticket", pipeline, ["api"], T, "claude", "42", Project: "alpha", Platform: "github");

    [Fact]
    public async Task RunAttemptProjection_ResumedRun_KeepsTheFirstCause()
    {
        await ApplyAsync(Fetched(new AttemptCause(AttemptChannels.PullRequest, "alice", T,
            new AttemptSource(AttemptSourceKinds.Review, "https://github.com/o/api/pull/4", "PRR_1"))));
        await ApplyAsync(Fetched(new AttemptCause(AttemptChannels.Retrigger), state: "In Progress"));

        var row = (await RowsAsync()).Single();
        row.CauseChannel.Should().Be(AttemptChannels.PullRequest);
        row.CauseActor.Should().Be("alice");
        row.SourceUrl.Should().Be("https://github.com/o/api/pull/4");
        row.SourceId.Should().Be("PRR_1");
        row.TrackerState.Should().Be("Ready");
        row.WebUrl.Should().Be("https://github.com/o/api/issues/42");
    }

    [Fact]
    public async Task RunAttemptProjection_RedeliveredEvents_WriteOneRow()
    {
        var fetched = Fetched(new AttemptCause(AttemptChannels.Pickup));
        var published = new SpecSetPublishedEvent(RunId, T, 1, "initial", "2026-10-13-1234", [], Committed: true);
        await ApplyAsync(fetched, 1);
        await ApplyAsync(published, 2);
        await ApplyAsync(fetched, 1);
        await ApplyAsync(published, 2);

        (await RowsAsync()).Should().ContainSingle().Which.RevisionNumber.Should().Be(1);
    }

    [Fact]
    public async Task RunAttemptProjection_PublishEvent_RecordsRevisionCarriedIdsAndApprovalInstant()
    {
        await ApplyAsync(new SpecSetPublishedEvent(RunId, T, 1, "initial", "2026-10-13-1234", [], Committed: true));
        await ApplyAsync(new SpecSetPublishedEvent(RunId, T.AddMinutes(5), 2, "ticket edited", "2026-10-13-1234",
            ["2026-10-13-1234a"], T.AddMinutes(-10), "session-7", "carol", Committed: true));

        var row = (await RowsAsync()).Single();
        row.RevisionNumber.Should().Be(2);
        row.RevisionCause.Should().Be("ticket edited");
        row.SeriesId.Should().Be("2026-10-13-1234");
        row.CarriedSpecIdsJson.Should().Be("[\"2026-10-13-1234a\"]");
        row.ApprovalAt.Should().Be(T.AddMinutes(-10));
        row.ApprovalConversation.Should().Be("session-7");
        row.ApprovalPrincipal.Should().Be("carol");
    }

    [Fact]
    public async Task RunAttemptProjection_HandbackOnlyPublish_LeavesRevisionEmpty()
    {
        await ApplyAsync(Fetched(new AttemptCause(AttemptChannels.Pickup)));
        await ApplyAsync(new SpecSetPublishedEvent(RunId, T));

        var row = (await RowsAsync()).Single();
        row.RevisionNumber.Should().BeNull();
        row.ApprovalAt.Should().BeNull();
    }

    [Fact]
    public async Task RunDeletion_DeletesTheRunAttemptRow()
    {
        await ApplyAsync(Started());
        await ApplyAsync(Fetched(new AttemptCause(AttemptChannels.Pickup)));

        await using (var ctx = new AgentSmithDbContext(Options()))
            await new RunDeletionRepository(ctx).DeleteAsync(RunId, CancellationToken.None);

        (await RowsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RunStarted_NewCodeRunOrPromotedReservation_PublishesTicketActivity_RelaunchAndReplayDoNot()
    {
        await ApplyAsync(Started());
        await ApplyAsync(Started()); // a replay on the running row
        await ApplyAsync(Started("2026-10-13T0901-bbbb", pipeline: "pr-review"));
        await using (var ctx = new AgentSmithDbContext(Options()))
        {
            ctx.Add(new Run { Id = "2026-10-13T0902-cccc", Project = "alpha", TicketId = "42", Pipeline = "code", Status = RunStatuses.Queued, Platform = "github" });
            ctx.Add(new Run { Id = "2026-10-13T0903-dddd", Project = "alpha", TicketId = "42", Pipeline = "code", Status = RunStatuses.WaitingForInput, Platform = "github" });
            await ctx.SaveChangesAsync();
        }
        await ApplyAsync(Started("2026-10-13T0902-cccc"));
        await ApplyAsync(Started("2026-10-13T0903-dddd"));

        _activity.Verify(a => a.ChangedAsync("alpha", "github-42", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
