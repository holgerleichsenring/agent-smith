using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Events.Projection;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailRow = AgentSmith.Infrastructure.Persistence.Entities.RunEvent;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-26-0f19c: where a new holder of the projection lease starts reading a run's stream again —
/// after the highest trail row written in stream order, from the start when there is none, and not at
/// all when the trail holds only positions counted before they came from the stream.
/// </summary>
public sealed class RunProjectionResumePointTests : IDisposable
{
    private const string RunId = "run-0f19c";
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task After_NoTrail_ReadsFromTheStart() =>
        (await AfterAsync()).Should().Be(RunProjectionResumePoint.FromStart);

    [Fact]
    public async Task After_StreamPositions_ResumesAfterTheHighestOrderedOne()
    {
        Seed((1_760_000_000_001_000, nameof(EventType.RunStarted)), (1_760_000_000_002_000, nameof(EventType.LlmCallFinished)),
            (1_760_000_000_009_000, nameof(EventType.SandboxResult)));

        (await AfterAsync()).Should().Be("1760000000002-0", "a sandbox result is written ahead of the ordered flush");
    }

    [Fact]
    public async Task After_OnlyCountedPositions_HasNothingToResumeAfter()
    {
        Seed((0, nameof(EventType.RunStarted)), (1, nameof(EventType.LlmCallFinished)));

        (await AfterAsync()).Should().BeNull("a run from before positions came from the stream is not read again");
    }

    private Task<string?> AfterAsync()
    {
        var services = new ServiceCollection()
            .AddScoped<IUnitOfWork>(_ => MigratedStoreTemplate.Context(_connection)).BuildServiceProvider();
        return new RunProjectionResumePoint(services.GetRequiredService<IServiceScopeFactory>()).AfterAsync(RunId, default);
    }

    private void Seed(params (long Seq, string Type)[] rows)
    {
        using var ctx = MigratedStoreTemplate.Context(_connection);
        foreach (var (seq, type) in rows)
            ctx.Set<TrailRow>().Add(new TrailRow { RunId = RunId, Seq = seq, Type = type, Timestamp = DateTimeOffset.UtcNow });
        ctx.SaveChanges();
    }
}
