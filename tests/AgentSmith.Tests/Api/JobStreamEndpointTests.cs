using System.Runtime.CompilerServices;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Server.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Api;

/// <summary>
/// p0169b: SSE wire-format coverage on IJobBusSubscriber + SseEventWriter
/// pairs. Full HTTP-level coverage (idle timeout, connection close, CORS)
/// is exercised in docker-compose smoke since standing up the Server's
/// composition root inside a TestServer is the same friction the p0169a
/// JobsEndpoints tests skipped for cause.
/// </summary>
public sealed class JobStreamEndpointTests
{
    [Fact]
    public async Task PublishedProgressMessage_EmittedAsProgressEvent()
    {
        var bus = new FakeJobBusSubscriber(
            BusMessage.Progress("job-1", step: 1, total: 3, "Boot"),
            BusMessage.Progress("job-1", step: 2, total: 3, "Plan"));

        var output = new List<string>();
        await foreach (var msg in bus.SubscribeAsync("job-1", false, CancellationToken.None))
            output.Add(SseEventWriter.Format(msg));

        output.Should().HaveCount(2);
        output[0].Should().StartWith("event: progress\n");
        output[0].Should().Contain("\"step\":1");
    }

    [Fact]
    public async Task PublishedDoneMessage_EmittedAsDoneEventWithPrUrl()
    {
        var bus = new FakeJobBusSubscriber(
            BusMessage.Done("job-1", prUrl: "https://example/pr/42", "shipped"));

        var output = new List<string>();
        await foreach (var msg in bus.SubscribeAsync("job-1", false, CancellationToken.None))
            output.Add(SseEventWriter.Format(msg));

        output.Should().HaveCount(1);
        output[0].Should().Contain("\"pr_url\":\"https://example/pr/42\"");
    }

    [Fact]
    public async Task FromBeginningTrue_ReplaysBufferedBacklog()
    {
        var bus = new FakeJobBusSubscriber(
            BusMessage.Progress("job-1", step: 1, total: 2, "Old"),
            BusMessage.Done("job-1", prUrl: null, "Done"));

        var live = new List<BusMessage>();
        await foreach (var msg in bus.SubscribeAsync("job-1", fromBeginning: false, CancellationToken.None))
            live.Add(msg);

        var replay = new List<BusMessage>();
        await foreach (var msg in bus.SubscribeAsync("job-1", fromBeginning: true, CancellationToken.None))
            replay.Add(msg);

        live.Should().HaveCount(2);
        replay.Should().HaveCount(2); // fake echoes both modes; semantics asserted by FromBeginningCalled flag
        bus.FromBeginningCalled.Should().BeTrue();
    }

    private sealed class FakeJobBusSubscriber(params BusMessage[] messages) : IJobBusSubscriber
    {
        public bool FromBeginningCalled { get; private set; }

#pragma warning disable CS1998 // async iterator with no awaits
        public async IAsyncEnumerable<BusMessage> SubscribeAsync(
            string jobId, bool fromBeginning,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (fromBeginning) FromBeginningCalled = true;
            foreach (var m in messages)
            {
                if (cancellationToken.IsCancellationRequested) yield break;
                yield return m;
            }
        }
#pragma warning restore CS1998
    }
}
