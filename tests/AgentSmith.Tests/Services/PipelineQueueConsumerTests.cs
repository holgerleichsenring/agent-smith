using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class PipelineQueueConsumerTests
{
    [Fact]
    public async Task RunAsync_ConsumesRequest_DispatchesViaJobDispatcher()
    {
        var dispatched = new List<PipelineRequest>();
        var dispatcher = new Mock<IPipelineJobDispatcher>();
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRequest, CancellationToken>((req, _) => dispatched.Add(req))
            .ReturnsAsync("job-12345678");

        var queue = new StubQueue([
            new PipelineRequest("proj-a", "fix-bug", new TicketId("11"), Headless: true),
            new PipelineRequest("proj-b", "fix-bug", new TicketId("12"), Headless: true),
        ]);

        var services = new ServiceCollection();
        services.AddSingleton(dispatcher.Object);
        var provider = services.BuildServiceProvider();

        var consumer = new PipelineQueueConsumer(
            provider, queue, configPath: "/tmp/config.yml",
            maxParallelJobs: 2, shutdownGraceSeconds: 1,
            NullLogger<PipelineQueueConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await consumer.RunAsync(cts.Token);

        dispatched.Should().HaveCount(2);
        dispatched.Select(r => r.ProjectName).Should().BeEquivalentTo(["proj-a", "proj-b"]);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_DispatcherThrows_LogsAndContinues()
    {
        var dispatcher = new Mock<IPipelineJobDispatcher>();
        var calls = 0;
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Returns<PipelineRequest, CancellationToken>((_, _) =>
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("spawn failed");
                return Task.FromResult("job-second");
            });

        var queue = new StubQueue([
            new PipelineRequest("proj-a", "fix-bug", new TicketId("1"), Headless: true),
            new PipelineRequest("proj-b", "fix-bug", new TicketId("2"), Headless: true),
        ]);

        var services = new ServiceCollection();
        services.AddSingleton(dispatcher.Object);
        var provider = services.BuildServiceProvider();

        var consumer = new PipelineQueueConsumer(
            provider, queue, configPath: "/tmp/config.yml",
            maxParallelJobs: 1, shutdownGraceSeconds: 1,
            NullLogger<PipelineQueueConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await consumer.RunAsync(cts.Token);

        calls.Should().Be(2);
    }

    private sealed class StubQueue(IReadOnlyList<PipelineRequest> items) : IRedisJobQueue
    {
        public Task EnqueueAsync(PipelineRequest request, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<long> LenAsync(CancellationToken cancellationToken) =>
            Task.FromResult((long)items.Count);

        public async IAsyncEnumerable<PipelineRequest> ConsumeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested) yield break;
                yield return item;
                await Task.Yield();
            }
            // Stay open until cancellation so the consumer's await foreach awaits.
            await Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }
}
