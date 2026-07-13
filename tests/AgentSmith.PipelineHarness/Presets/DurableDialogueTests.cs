using AgentSmith.Application.Services;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;
using SkillsConfig = AgentSmith.Contracts.Models.Configuration.SkillsConfig;
using SkillsSourceMode = AgentSmith.Contracts.Models.Configuration.SkillsSourceMode;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0327: the durable-dialogue proof, LLM-free through the REAL composition.
/// A fix-bug run with an interactive approval crosses the (zero-width fixture)
/// hot window, checkpoints, and parks as waiting_for_input; the orchestrator
/// "restarts" (first harness disposed, a second built over the same SQLite
/// file); the operator's answer lands in the durable inbox; the sweeper turns
/// it into a capacity-queue resume entry; the REAL pump launches it; and the
/// resumed request completes the run — ONE run record, correct result.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DurableDialogueTests
{
    private const string Project = "fixture-fix-bug";
    private const string TicketNumber = "7";

    [Fact]
    public async Task FixBug_CheckpointMidApproval_RestartAnswerResume_OneRunRecord()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"agentsmith-harness-{Guid.NewGuid():N}.db");
        var jobQueue = new RecordingJobQueue();
        try
        {
            // ---- Act 1: the run parks at the approval question ----
            string runId;
            await using (var first = BuildHarness(dbPath, jobQueue))
            {
                await MigrateAsync(first);
                var result = await ExecuteAsync(first, Request(runId: null));
                result.IsSuccess.Should().BeTrue("parking is a clean halt, not a failure");
                runId = SingleRun(dbPath).Id;
            }

            AssertParked(dbPath);
            var parked = SingleCheckpoint(dbPath);

            // ---- Act 2: "restart" — a fresh composition over the same DB ----
            await using var second = BuildHarness(dbPath, jobQueue);
            second.ChatClient
                .EnqueueToolCall("write_file", """{"path":"csharp-fixture/src/Patch.cs","content":"// fix"}""")
                .EnqueueText("""Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"patched"}""");

            // The operator answers AFTER the restart — durable inbox first.
            await second.Services.GetRequiredService<IDialogueTransport>().PublishAnswerAsync(
                parked.DialogueJobId,
                new DialogAnswer(parked.QuestionId, "approve", null, DateTimeOffset.UtcNow, "@operator"),
                CancellationToken.None);

            // The sweeper turns inbox row + checkpoint into a resume queue entry…
            (await second.Services.GetRequiredService<DialogueResumeSweeper>()
                .ScanOnceAsync(CancellationToken.None)).Should().Be(1);
            // …and the REAL pump launches it (lease + direct job enqueue, no
            // trigger-status re-validation for a mid-run ticket).
            await BuildPump(second, jobQueue).TickAsync(CancellationToken.None);

            var resumeRequest = jobQueue.DequeueViaJsonRoundTrip();
            resumeRequest.RunId.Should().Be(runId, "the resume reuses the reserved run row");
            resumeRequest.Context.Should().ContainKey("ResumeCheckpoint");

            // ---- Act 3: the resumed worker re-enters at the cursor ----
            var resumed = await ExecuteAsync(second, resumeRequest);

            resumed.IsSuccess.Should().BeTrue("the resumed run must complete");
            second.StubSandboxFactory!.Spawned.Should().NotBeEmpty(
                "resume re-provisions fresh sandboxes — the checkpointed run held none");
            AssertOneCompletedRun(dbPath, runId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    // ---- assertions ----

    private static void AssertParked(string dbPath)
    {
        using var ctx = Db(dbPath);
        var run = ctx.Runs.Single();
        run.Status.Should().Be("waiting_for_input");
        run.FinishedAt.Should().BeNull("waiting is an active state — the run is NOT over");
        var checkpoint = ctx.RunCheckpoints.Single();
        checkpoint.ResumedAt.Should().BeNull();
        checkpoint.QuestionJson.Should().Contain("Approve");
        checkpoint.RemainingCommandsJson.Should().Contain("ApprovalCommand",
            "the cursor re-enters AT the asking step");
        checkpoint.RemainingCommandsJson.Should().Contain("CheckoutSourceCommand",
            "the resume re-provisions the working tree first — sandboxes are cattle");
    }

    private static void AssertOneCompletedRun(string dbPath, string runId)
    {
        using var ctx = Db(dbPath);
        var run = ctx.Runs.Single(); // checkpoint/resume must never mint a second run row
        run.Id.Should().Be(runId);
        run.Status.Should().Be("success");
        run.FinishedAt.Should().NotBeNull();
        ctx.RunCheckpoints.Single().ResumedAt.Should().NotBeNull();
        ctx.QueuedTickets.Should().BeEmpty("the launched resume entry is consumed");
    }

    private static RunCheckpoint SingleCheckpoint(string dbPath)
    {
        using var ctx = Db(dbPath);
        return ctx.RunCheckpoints.Single();
    }

    private static Run SingleRun(string dbPath)
    {
        using var ctx = Db(dbPath);
        return ctx.Runs.Single();
    }

    // ---- plumbing ----

    private static RealCompositionHarness BuildHarness(string dbPath, RecordingJobQueue jobQueue) =>
        RealCompositionHarness.Build(
            FixturePaths.For("agentsmith-dialogue.yml"), SandboxBackend.Stub, session: null,
            SkillsBackend.Fixture, services =>
            {
                // Shared SQLite FILE: the durable state that survives the "restart".
                services.RemoveAll<DbContextOptions<AgentSmithDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<AgentSmithDbContext>();
                services.AddDbContext<AgentSmithDbContext>(b => b.UseSqlite($"Data Source={dbPath}"));
                // Fast tier has no Redis: project events synchronously into the DB
                // (production: RedisEventPublisher → RunDbProjector, same applier).
                services.RemoveAll<IEventPublisher>();
                services.AddSingleton<IEventPublisher>(sp =>
                    new ProjectingEventPublisher(sp.GetRequiredService<IServiceScopeFactory>()));
                // The use case resolves the skills catalog — network boundary, stubbed.
                services.RemoveAll<ISkillsCatalogResolver>();
                services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
                // The production server registration: durable inbox first, hot
                // stream second. The hot stream is irrelevant here (no live wait
                // across the restart), so the inner transport is a mock.
                services.RemoveAll<IDialogueTransport>();
                services.AddSingleton<IDialogueTransport>(sp =>
                    new Server.Services.Dialogue.DurableDialogueTransport(
                        Mock.Of<IDialogueTransport>(),
                        sp.GetRequiredService<IDialogueAnswerInbox>()));
                // The Redis job queue is the launch channel — recorded + JSON
                // round-tripped so the resume payload takes the production shape.
                services.RemoveAll<IRedisJobQueue>();
                services.AddSingleton<IRedisJobQueue>(jobQueue);
            });

    private static async Task MigrateAsync(RealCompositionHarness harness)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>().Database.MigrateAsync();
    }

    private static async Task<CommandResult> ExecuteAsync(
        RealCompositionHarness harness, PipelineRequest request)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ExecutePipelineUseCase>()
            .ExecuteAsync(request, FixturePaths.For("agentsmith-dialogue.yml"), CancellationToken.None);
    }

    // Headless=false so the approval actually asks — the checkpointable shape.
    private static PipelineRequest Request(string? runId) => new(
        Project, "fix-bug", TicketId: new TicketId(TicketNumber), Headless: false, RunId: runId);

    private static CapacityQueuePump BuildPump(RealCompositionHarness harness, RecordingJobQueue jobQueue)
    {
        var sp = harness.Services;
        return new CapacityQueuePump(
            sp.GetRequiredService<ICapacityQueue>(),
            sp.GetRequiredService<ITicketClaimService>(),
            sp.GetRequiredService<AgentSmith.Contracts.Providers.ITicketProviderFactory>(),
            sp.GetRequiredService<AgentSmith.Application.Services.Sandbox.ISandboxResourceResolver>(),
            sp.GetRequiredService<AgentSmith.Application.Services.Orchestrator.IOrchestratorResourceResolver>(),
            sp.GetRequiredService<AgentSmith.Contracts.Sandbox.ISandboxCapacityProbe>(),
            sp.GetRequiredService<IEventPublisher>(),
            sp.GetRequiredService<IRunCancelStateReader>(),
            new ResumeRunLauncher(
                sp, sp.GetRequiredService<IActiveRunLease>(), jobQueue,
                sp.GetRequiredService<ICapacityQueue>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ResumeRunLauncher>>()),
            sp.GetRequiredService<IConfigurationLoader>(),
            FixturePaths.For("agentsmith-dialogue.yml"),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CapacityQueuePump>>());
    }

    private static AgentSmithDbContext Db(string dbPath) => new(
        new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseSqlite($"Data Source={dbPath}").Options);

    /// <summary>Synchronous event → DB projection (the fast tier has no Redis;
    /// production routes the same events through RunDbProjector's applier).</summary>
    private sealed class ProjectingEventPublisher(IServiceScopeFactory scopeFactory) : IEventPublisher
    {
        private readonly RunEventApplier _applier = new();

        public async Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
        {
            using var scope = scopeFactory.CreateScope();
            var uow = scope.ServiceProvider
                .GetRequiredService<AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork>();
            await _applier.ApplyAsync(uow, runEvent, cancellationToken);
        }
    }

    /// <summary>The Redis job-list boundary: records enqueued requests and hands
    /// them back through the SAME JSON round-trip RedisJobQueue performs, so
    /// resume-context values arrive as JsonElement exactly like production.</summary>
    private sealed class RecordingJobQueue : IRedisJobQueue
    {
        private readonly List<string> _enqueued = [];

        public Task EnqueueAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            _enqueued.Add(System.Text.Json.JsonSerializer.Serialize(request));
            return Task.CompletedTask;
        }

        public PipelineRequest DequeueViaJsonRoundTrip()
        {
            _enqueued.Should().NotBeEmpty("the pump must have enqueued the resume request");
            var json = _enqueued[^1];
            return System.Text.Json.JsonSerializer.Deserialize<PipelineRequest>(json)!;
        }

        public async IAsyncEnumerable<PipelineRequest> ConsumeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<long> LenAsync(CancellationToken cancellationToken) =>
            Task.FromResult((long)_enqueued.Count);
    }

    private sealed class StubCatalogResolver : ISkillsCatalogResolver
    {
        public Task<CatalogResolution> EnsureResolvedAsync(
            SkillsConfig config, CancellationToken cancellationToken) =>
            Task.FromResult(new CatalogResolution(
                "/tmp/agentsmith-harness/empty-catalog", "harness",
                SkillsSourceMode.Default, "https://stub.test/catalog", FromCache: true));
    }
}
