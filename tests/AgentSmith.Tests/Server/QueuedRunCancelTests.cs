using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0320c: cancelling a QUEUED run is pure bookkeeping — nothing is executing,
/// so no registry roundtrip: the queue entry is deleted and the row finishes
/// "cancelled" via the terminal event.
/// </summary>
public sealed class QueuedRunCancelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ICapacityQueue _queue;
    private readonly List<RunEvent> _published = [];
    private readonly IEventPublisher _events;

    public QueuedRunCancelTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.Database.Migrate();
        _queue = BuildDbQueue(_connection);

        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((ev, _) => _published.Add(ev))
            .Returns(Task.CompletedTask);
        _events = events.Object;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Cancel_QueuedRun_RemovesEntry_MarksCancelled()
    {
        var reserved = await _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", "42", "fix-bug", "github",
            "2026-07-10T12-00-00-a1b2", "waiting for sandbox capacity",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var handled = await RunControlEndpoints.TryCancelQueuedAsync(
            reserved, NewRepository(), _queue, _events, CancellationToken.None);

        handled.Should().BeTrue();
        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.QueuedTickets.Should().BeEmpty();
        var finished = _published.OfType<RunFinishedEvent>().Single();
        finished.RunId.Should().Be(reserved);
        finished.Status.Should().Be("cancelled");

        // Project the terminal event — the queued row is finished as cancelled.
        await new RunEventApplier().ApplyAsync(
            new AgentSmithDbContext(Options()), finished, CancellationToken.None);
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == reserved);
        run.Status.Should().Be("cancelled");
        run.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_RunningRun_NotHandledHere_FallsThroughToRegistryPath()
    {
        using (var ctx = new AgentSmithDbContext(Options()))
        {
            ctx.Runs.Add(new AgentSmith.Infrastructure.Persistence.Entities.Run
            {
                Id = "run-live", Project = "p1", Pipeline = "fix-bug",
                TicketId = "42", Status = "running", StartedAt = DateTimeOffset.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        var handled = await RunControlEndpoints.TryCancelQueuedAsync(
            "run-live", NewRepository(), _queue, _events, CancellationToken.None);

        handled.Should().BeFalse("a live run cancels through the registry, not the queue path");
        _published.Should().BeEmpty();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private RunRepository NewRepository() => new(new AgentSmithDbContext(Options()));

    private static ICapacityQueue BuildDbQueue(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options));
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<QueuedTicketRepository>();
        return new DbCapacityQueue(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }
}
