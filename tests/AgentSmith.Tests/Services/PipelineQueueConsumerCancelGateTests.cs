using System.Runtime.CompilerServices;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0330: the queue consumer checks the PERSISTED cancel flag before executing —
/// a cancel that landed while the request sat in the Redis job queue (or on the
/// semaphore) short-circuits to RunFinished(cancelled) + lease release, and the
/// pipeline never starts. The provider deliberately does NOT register
/// ExecutePipelineUseCase: if the gate leaked through, resolution would throw
/// and no terminal event would be published.
/// </summary>
public sealed class PipelineQueueConsumerCancelGateTests
{
    private readonly List<RunEvent> _published = [];
    private readonly Mock<IActiveRunLease> _lease = new();

    [Fact]
    public async Task QueueConsumer_CancelledBeforeStart_ShortCircuitsCancelled()
    {
        var request = new PipelineRequest(
            "p1", "fix-bug", new TicketId("42"), RunId: "run-cancelled");
        var consumer = NewConsumer(request, cancelRequested: true);

        await consumer.RunAsync(CancellationToken.None);

        var finished = _published.OfType<RunFinishedEvent>().Single();
        finished.RunId.Should().Be("run-cancelled");
        finished.Status.Should().Be("cancelled");
        _lease.Verify(l => l.ReleaseAsync(
            "p1", new TicketId("42"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueueConsumer_NotCancelled_ProceedsToExecution()
    {
        var request = new PipelineRequest(
            "p1", "fix-bug", new TicketId("42"), RunId: "run-live");
        var consumer = NewConsumer(request, cancelRequested: false);

        // Proceeding means resolving ExecutePipelineUseCase, which this minimal
        // provider does not register — the consumer logs the error and publishes
        // nothing (proof the gate did NOT short-circuit a live run).
        await consumer.RunAsync(CancellationToken.None);

        _published.Should().BeEmpty();
        _lease.Verify(l => l.ReleaseAsync(
            It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private PipelineQueueConsumer NewConsumer(PipelineRequest request, bool cancelRequested)
    {
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((ev, _) => _published.Add(ev))
            .Returns(Task.CompletedTask);
        var services = new ServiceCollection();
        services.AddSingleton(events.Object);
        services.AddSingleton(_lease.Object);
        var reader = new Mock<IRunCancelStateReader>();
        reader.Setup(r => r.IsCancelRequestedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cancelRequested);
        return new PipelineQueueConsumer(
            services.BuildServiceProvider(), new SingleShotQueue(request), reader.Object,
            "config.yaml", maxParallelJobs: 1, shutdownGraceSeconds: 5,
            NullLogger<PipelineQueueConsumer>.Instance);
    }

    // Yields exactly one request, then ends — RunAsync drains in-flight work and
    // returns, so the tests need no timing games.
    private sealed class SingleShotQueue(PipelineRequest request) : IRedisJobQueue
    {
        public Task EnqueueAsync(PipelineRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public async IAsyncEnumerable<PipelineRequest> ConsumeAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return request;
            await Task.CompletedTask;
        }

        public Task<long> LenAsync(CancellationToken cancellationToken) => Task.FromResult(0L);
    }
}
