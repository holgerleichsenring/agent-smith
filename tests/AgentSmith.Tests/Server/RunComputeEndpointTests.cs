using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using DialogueAnswerEntry = AgentSmith.Infrastructure.Persistence.Entities.DialogueAnswerEntry;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-13-9a59i: GET /api/runs/{runId}/compute serves every spawn of a run in spawn order with its own
/// times and end, and names the sandboxes no row records. Driven through the composition root's projection
/// registration over a migrated SQLite store.
/// </summary>
public sealed class RunComputeEndpointTests : IDisposable
{
    private const string RunId = "2026-10-13T0900-compute";
    private static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;
    private long _seq;

    public RunComputeEndpointTests()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options));
        services.AddSingleton(Mock.Of<ITicketActivityNudge>());
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton<RunComputeReader>();
        services.AddSingleton<RunSegmentsReader>().AddSingleton<RunParkReader>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RunComputeEndpoint_RespawnedPod_ShowsEachSpawnWithItsTimes()
    {
        await ApplyAsync(
            new RunStartedEvent(RunId, "ticket", "code", ["api"], T, "claude", "42"),
            Created(minute: 1), Disposed(minute: 5), Finished("waiting_for_input", minute: 6),
            Created(minute: 20), Disposed(minute: 40), Finished("success", minute: 41));

        var view = await ReadAsync();

        view.Spawns.Should().HaveCount(2);
        view.Spawns.Select(s => (s.Repo, s.SpawnedAt, s.DisposedAt, s.Memory, s.EndReason)).Should().Equal(
            ("api", T.AddMinutes(1), T.AddMinutes(5), "2Gi", "released-at-park"),
            ("api", T.AddMinutes(20), T.AddMinutes(40), "2Gi", "released-at-end"));
    }

    [Fact]
    public async Task RunComputeEndpoint_NamesUncountedSandboxes()
    {
        await ApplyAsync(new RunStartedEvent(RunId, "ticket", "code", ["api"], T, "claude", "42"));

        var view = await ReadAsync();

        view.Spawns.Should().BeEmpty();
        view.Uncounted.Select(u => u.Kind).Should().Equal("source-scope", "browser", "preflight");
        view.Uncounted.Should().OnlyContain(u => u.Label.Length > 0);
    }

    [Fact]
    public async Task RunComputeEndpoint_ServesSegments()
    {
        await ApplyAsync(
            new RunStartedEvent(RunId, "ticket", "code", ["api"], T, "claude", "42"),
            new LlmCallFinishedEvent(RunId, "gpt-4o", "master", 10, 5, 0.01m, 4_000, T.AddMinutes(2)),
            Parked("q1", minute: 3), Finished("waiting_for_input", minute: 4),
            new RunStartedEvent(RunId, "ticket", "code", ["api"], T.AddMinutes(20), "claude", "42"),
            new LlmCallFinishedEvent(RunId, "gpt-4o", "master", 10, 5, 0.01m, 6_000, T.AddMinutes(22)),
            Finished("success", minute: 30));
        using (var scope = _provider.CreateScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            uow.Add(new DialogueAnswerEntry
            {
                DialogueJobId = RunId, QuestionId = "q1", Answer = "yes", AnsweredBy = "alice", AnsweredAt = T.AddMinutes(15),
            });
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        var view = await ReadAsync();

        view.Segments.Select(s => (s.StartedAt, s.EndedAt, s.EndStatus, s.ModelMs)).Should().Equal(
            (T, T.AddMinutes(4), "waiting_for_input", 4_000L),
            (T.AddMinutes(20), T.AddMinutes(30), "success", 6_000L));
        view.Segments[0].Park.Should().Be(new RunComputeParkView(
            RunParkKinds.Question, T.AddMinutes(3), "q1", T.AddMinutes(15), "alice", T.AddMinutes(20)));
        view.UnattributedCalls.Should().Be(0);
    }

    private static RunCheckpointedEvent Parked(string questionId, int minute) =>
        new(RunId, "alpha", "42", "github", "code", RunId, questionId,
            System.Text.Json.JsonSerializer.Serialize(new AgentSmith.Contracts.Dialogue.DialogQuestion(
                questionId, AgentSmith.Contracts.Dialogue.QuestionType.FreeText, "Which client?", null, null, null,
                TimeSpan.FromHours(1))),
            "[]", "{}", 1, T.AddMinutes(minute), T.AddHours(1), T.AddMinutes(minute));

    [Fact]
    public async Task RunComputeEndpoint_UnknownRun_IsNotFound()
    {
        var result = await RunComputeEndpoints.ComputeAsync(
            "no-such-run", _provider.GetRequiredService<RunComputeReader>(), CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    private async Task<RunComputeRecordView> ReadAsync()
    {
        var result = await RunComputeEndpoints.ComputeAsync(
            RunId, _provider.GetRequiredService<RunComputeReader>(), CancellationToken.None);
        return result.Should().BeOfType<Ok<RunComputeRecordView>>().Subject.Value!;
    }

    private static SandboxCreatedEvent Created(int minute) =>
        new(RunId, "api", "node:20", "typescript", T.AddMinutes(minute), MemoryRequest: "2Gi");

    private static SandboxDisposedEvent Disposed(int minute) =>
        new(RunId, "api", null, T.AddMinutes(minute), "running");

    private static RunFinishedEvent Finished(string status, int minute) =>
        new(RunId, status, null, "s", T.AddMinutes(minute));

    private async Task ApplyAsync(params RunEvent[] events)
    {
        foreach (var ev in events)
        {
            using var scope = _provider.CreateScope();
            await _provider.GetRequiredService<RunEventApplier>().ApplyAsync(
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), ev, ++_seq, CancellationToken.None);
        }
    }
}
