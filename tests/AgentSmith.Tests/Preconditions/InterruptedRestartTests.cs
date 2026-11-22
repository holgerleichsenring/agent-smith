using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Preconditions;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Preconditions;

/// <summary>
/// 2026-11-22-6d85c: an attempt a lost process ended is retried unchanged once by itself — the reader lists the code heads
/// cancelled with an owed reason, not one already retried or itself the restart, and the pass asks Retry as agent-smith.
/// </summary>
public sealed class InterruptedRestartTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private static readonly DateTimeOffset T0 = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => _connection.Dispose();

    private AgentSmithDbContext Db() => new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private static Run Row(string id, string ticket, string status, string? reason = null, string pipeline = "code") => new()
    {
        Id = id, Project = "p", TicketId = ticket, Pipeline = pipeline, Status = status, StartedAt = T0,
        FinishedAt = status is RunStatuses.Queued or RunStatuses.Running ? null : T0.AddMinutes(1), CancelReason = reason,
    };

    [Fact]
    public async Task List_NewestCodeAttemptCancelledByALostProcess_Listed()
    {
        var listed = await ListAsync(T0,
            Row("2026-11-22T09-00-00-a", "1", "cancelled", OwedWorkCancel.Interrupted),
            Row("2026-11-22T09-00-00-b", "2", "cancelled", OwedWorkCancel.StaleLeaseReaped),
            Row("2026-11-22T09-00-01-b", "2", RunStatuses.Queued),
            Row("2026-11-22T09-00-00-c", "3", RunStatuses.Failed),
            Row("2026-11-22T09-00-00-d", "4", "cancelled", "operator"),
            Row("2026-11-22T09-00-00-e", "5", "cancelled", OwedWorkCancel.Interrupted),
            Row("2026-11-22T09-00-01-e", "5", RunStatuses.Running),
            Row("2026-11-22T09-00-00-f", "6", "cancelled", OwedWorkCancel.Interrupted, pipeline: "spec-dialog"));

        listed.Select(a => a.TicketId).Should().Equal("1", "2");
    }

    [Fact]
    public async Task List_AlreadyRetried_ItselfTheRestart_OrBeforeTheHorizon_Left()
    {
        await using (var db = Db())
        {
            db.Add(new RunRetryRequest { RunId = "2026-11-22T09-00-00-a", RequestedBy = "ada", RequestedAt = T0 });
            db.Add(new RunAttempt { RunId = "2026-11-22T09-00-00-b", CauseChannel = AttemptChannels.Retry, CauseActor = AttemptChannels.RestartActor });
            await db.SaveChangesAsync();
        }

        var listed = await ListAsync(T0.AddMinutes(2),
            Row("2026-11-22T09-00-00-a", "1", "cancelled", OwedWorkCancel.Interrupted),
            Row("2026-11-22T09-00-00-b", "2", "cancelled", OwedWorkCancel.Interrupted),
            Row("2026-11-22T09-00-00-c", "3", "cancelled", OwedWorkCancel.Interrupted));

        listed.Should().BeEmpty("1 has a Retry pending, 2 was the one restart, 3 ended before the horizon");
    }

    [Fact]
    public async Task Pass_RetriesEachListedAttemptOnceAsAgentSmith_SkipsAnUnconfiguredProject()
    {
        var reader = new Mock<IInterruptedAttemptReader>();
        reader.Setup(r => r.ListAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new InterruptedAttempt("p", "1", "run-1"), new InterruptedAttempt("gone", "2", "run-2")]);
        var retry = new Mock<IRunRetry>();
        retry.Setup(r => r.RetryAsync("run-1", AttemptChannels.RestartActor, AttemptChannels.RestartActor, RetryOrigin.Restart, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RunRetryResult.Requested("1"));
        var services = new ServiceCollection().AddScoped(_ => retry.Object).BuildServiceProvider();
        var pass = new InterruptedRestartPass(reader.Object, services.GetRequiredService<IServiceScopeFactory>(),
            new InterruptedRestartWindow(TimeProvider.System), NullLogger<InterruptedRestartPass>.Instance);

        var asked = await pass.RunAsync(new AgentSmithConfig { Projects = { ["p"] = new ResolvedProject { Name = "p" } } }, default);

        asked.Should().Be(1);
        retry.Verify(r => r.RetryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<RetryOrigin>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Window_CoversAnEndWithinTheHorizonBeforeTheProcessStarted_NotOlderOnes()
    {
        var window = new InterruptedRestartWindow(TimeProvider.System);

        window.Covers(window.Since).Should().BeTrue();
        window.Covers(window.Since.AddSeconds(-1)).Should().BeFalse("an interruption from before the restart existed is a person's");
        window.Covers(null).Should().BeFalse();
    }

    private async Task<IReadOnlyList<InterruptedAttempt>> ListAsync(DateTimeOffset since, params Run[] rows)
    {
        await using (var db = Db())
        {
            db.AddRange(rows);
            await db.SaveChangesAsync();
        }
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Db());
        return await new DbInterruptedAttemptReader(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>())
            .ListAsync(since, CancellationToken.None);
    }
}
