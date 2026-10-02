using System.Text.Json;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-02-5ab2b: the operator's finding, through the REAL server composition. An init is
/// launched and its queue entry is lost — Redis flushed before any consumer popped it. The row
/// kept the request: the housekeeping sweeper pushes it again, the start gate claims it, and the
/// operator's auto-complete choice arrives with it; a second copy is dropped at the claim.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class QueuedRunRecoveryTests
{
    private const string RunId = "2026-10-02T20-00-00-5ab2";

    [Fact]
    public async Task LostQueueEntry_InMemoryQueueDropsTheRequest_SweeperRestartsItWithAutoCompleteKept()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"agentsmith-harness-{Guid.NewGuid():N}.db");
        var queue = new LosingJobQueue();
        try
        {
            await using var harness = QueuedRunRecoveryHarness.Build(dbPath, queue);
            await DurableDialogueHarness.MigrateAsync(harness);
            await QueuedRunRecoveryHarness.LaunchInitAsync(harness, RunId, autoComplete: true);
            queue.Pending.Should().Be(0, "the first push was lost");

            var sweeper = harness.Services.GetRequiredService<QueuedRunSweeper>();
            await QueuedRunRecoveryHarness.AgeRequestAsync(dbPath, RunId);
            await sweeper.RunOnceAsync(CancellationToken.None);
            await QueuedRunRecoveryHarness.AgeRequestAsync(dbPath, RunId);
            await sweeper.RunOnceAsync(CancellationToken.None);

            var gate = harness.Services.GetRequiredService<RunStartGate>();
            var first = queue.Pop();
            var second = queue.Pop();
            await using var started = await gate.ClaimAsync(first, CancellationToken.None);
            var duplicate = await gate.ClaimAsync(second, CancellationToken.None);

            started.Should().NotBeNull().And.Match<ClaimedRunStart>(s => s.IsBeating);
            duplicate.Should().BeNull("the copy pushed twice starts once");
            first.RunId.Should().Be(RunId);
            ((JsonElement)first.Context![ContextKeys.AutoCompletePullRequests]).GetBoolean().Should().BeTrue();
        }
        finally
        {
            QueuedRunRecoveryHarness.DeleteStore(dbPath);
        }
    }

    // The Redis list as a flush leaves it: the first push vanishes, later ones survive the same
    // JSON round trip RedisJobQueue performs.
    private sealed class LosingJobQueue : IRedisJobQueue
    {
        private readonly Queue<string> _entries = new();
        private bool _lostOne;

        public int Pending => _entries.Count;

        public Task EnqueueAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            if (_lostOne) _entries.Enqueue(JsonSerializer.Serialize(request));
            _lostOne = true;
            return Task.CompletedTask;
        }

        public PipelineRequest Pop() => JsonSerializer.Deserialize<PipelineRequest>(_entries.Dequeue())!;

        public async IAsyncEnumerable<PipelineRequest> ConsumeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<long> LenAsync(CancellationToken cancellationToken) => Task.FromResult((long)_entries.Count);
    }
}
