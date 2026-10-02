using System.Diagnostics;
using System.Text.Json;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Queue;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-02-5ab2b: the flush itself, on a real Redis. The test starts a Redis container of
/// its own — never the operator's — launches an init through the production dispatch onto the
/// production RedisJobQueue, flushes that Redis before anything pops, and lets the sweeper push
/// the request again twice. Of the two copies on the list exactly one is claimed to start, and
/// it carries the auto-complete choice. The container is removed afterwards.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class QueuedRunFlushDockerTests(ITestOutputHelper output)
{
    private const string RunId = "2026-10-02T20-30-00-5ab2";

    [Fact]
    public async Task Flush_QueuedInitAfterRealRedisFlush_StartsOnce()
    {
        if (!DockerAvailability.IsAvailable(out var detail))
        {
            output.WriteLine(DockerAvailability.CoverageNotExercised + " (" + detail + ")");
            return;
        }
        var container = Docker("run -d --rm -p 127.0.0.1::6379 redis:7-alpine");
        var dbPath = Path.Combine(Path.GetTempPath(), $"agentsmith-harness-{Guid.NewGuid():N}.db");
        try
        {
            using var redis = await ConnectAsync(container);
            var queue = new RedisJobQueue(redis, NullLogger<RedisJobQueue>.Instance);
            await using var harness = QueuedRunRecoveryHarness.Build(dbPath, queue);
            await DurableDialogueHarness.MigrateAsync(harness);
            await QueuedRunRecoveryHarness.LaunchInitAsync(harness, RunId, autoComplete: true);
            (await queue.LenAsync(CancellationToken.None)).Should().Be(1);

            await redis.GetServers().Single().FlushAllDatabasesAsync();
            (await queue.LenAsync(CancellationToken.None)).Should().Be(0, "the flush took the queue entry");
            await SweepTwiceAsync(harness, dbPath);

            var popped = await PopAsync(queue, 2);
            var gate = harness.Services.GetRequiredService<RunStartGate>();
            await using var started = await gate.ClaimAsync(popped[0], CancellationToken.None);
            var duplicate = await gate.ClaimAsync(popped[1], CancellationToken.None);
            started.Should().NotBeNull();
            duplicate.Should().BeNull("two copies of one request start once");
            ((JsonElement)popped[0].Context![ContextKeys.AutoCompletePullRequests]).GetBoolean().Should().BeTrue();
        }
        finally
        {
            Docker($"rm -f {container}");
            QueuedRunRecoveryHarness.DeleteStore(dbPath);
        }
    }

    private static async Task SweepTwiceAsync(RealCompositionHarness harness, string dbPath)
    {
        var sweeper = harness.Services.GetRequiredService<QueuedRunSweeper>();
        for (var i = 0; i < 2; i++)
        {
            await QueuedRunRecoveryHarness.AgeRequestAsync(dbPath, RunId);
            await sweeper.RunOnceAsync(CancellationToken.None);
        }
    }

    private static async Task<List<PipelineRequest>> PopAsync(RedisJobQueue queue, int count)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var popped = new List<PipelineRequest>();
        await foreach (var request in queue.ConsumeAsync(cts.Token))
        {
            popped.Add(request);
            if (popped.Count == count) break;
        }
        return popped;
    }

    private static async Task<ConnectionMultiplexer> ConnectAsync(string container)
    {
        var endpoint = Docker($"port {container} 6379").Split('\n')[0].Trim();
        var options = ConfigurationOptions.Parse(endpoint);
        options.AllowAdmin = true;
        options.AbortOnConnectFail = false;
        var redis = await ConnectionMultiplexer.ConnectAsync(options);
        for (var i = 0; i < 50 && !redis.IsConnected; i++) await Task.Delay(100);
        return redis;
    }

    private static string Docker(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("docker", arguments)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        })!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"docker {arguments} failed: {process.StandardError.ReadToEnd()}");
        return stdout.Trim();
    }
}
