using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Interceptors;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-28-ebb0: the three things PostgreSQL holds differently from SQLite and SQL Server,
/// each written through EF into a database of this run's own and read back through a second
/// context: an instant with a local offset, a NUL character, and a per-call money fraction.
/// Opt-in through <see cref="RequiresPostgresFactAttribute"/>.
/// </summary>
public sealed class PostgresStoreTests : IAsyncLifetime
{
    private const string RunId = "run-pg";
    private static readonly DateTimeOffset LocalAsked = new(2026, 10, 28, 15, 30, 0, TimeSpan.FromHours(2));

    private AgentSmithDbContext? _db;

    [RequiresPostgresFact]
    public async Task Write_AnInstantWithALocalOffset_ReadsBackAsTheSameInstant()
    {
        _db!.RunCheckpoints.Add(new RunCheckpoint
        {
            RunId = RunId, Project = "p", TicketId = "1", Pipeline = "code", DialogueJobId = "j", QuestionId = "q",
            QuestionJson = "{}", RemainingCommandsJson = "[]", ContextJson = "{}", AskedAt = LocalAsked,
            AnswerDeadlineAt = LocalAsked.AddHours(1),
        });
        await _db.SaveChangesAsync();

        await using var read = Reader();
        var stored = await read.RunCheckpoints.SingleAsync(c => c.RunId == RunId);
        stored.AskedAt.Should().Be(LocalAsked, "the same instant, whatever offset it is read back at");
        (await read.RunCheckpoints.CountAsync(c => c.AskedAt < LocalAsked.AddMinutes(1)))
            .Should().Be(1, "a local-offset query parameter compares as an instant too");
    }

    [RequiresPostgresFact]
    public async Task Write_TextCarryingANul_IsStoredWithTheReplacementCharacter()
    {
        _db!.RunArtifacts.Add(new RunArtifact { RunId = RunId, Kind = "stdout", Content = "before\0after" });
        var write = async () => await _db.SaveChangesAsync();

        await write.Should().NotThrowAsync("one NUL in a tool's output must not fail the write that records it");
        await using var read = Reader();
        (await read.RunArtifacts.SingleAsync(a => a.RunId == RunId)).Content
            .Should().Be($"before{PostgresNulTextInterceptor.Replacement}after");
    }

    [RequiresPostgresFact]
    public async Task Write_APerCallCostFraction_ReadsBackExactly()
    {
        _db!.Runs.Add(new Run
        {
            Id = RunId, Project = "p", Pipeline = "code", TicketId = RunId, Status = "success",
            StartedAt = DateTimeOffset.UtcNow, CostTotalUsd = 0.5843112m, BudgetCapUsd = 12.3456789m,
        });
        _db.RunLlmCalls.Add(new RunLlmCall { RunId = RunId, Model = "test-model", CostUsd = 0.0003219m });
        await _db.SaveChangesAsync();

        await using var read = Reader();
        (await read.RunLlmCalls.SingleAsync(c => c.RunId == RunId)).CostUsd.Should().Be(0.0003219m);
        var run = await read.Runs.SingleAsync(r => r.Id == RunId);
        run.CostTotalUsd.Should().Be(0.5843112m);
        run.BudgetCapUsd.Should().Be(12.3456789m);
    }

    public async Task InitializeAsync()
    {
        if (RequiresPostgresFactAttribute.ConnectionString is { } connection)
            _db = await ScratchPostgres.MigratedAsync(connection, "store");
    }

    public async Task DisposeAsync()
    {
        if (_db is null) return;
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private AgentSmithDbContext Reader() =>
        new(ScratchPostgres.Options(_db!.Database.GetConnectionString()!));
}
