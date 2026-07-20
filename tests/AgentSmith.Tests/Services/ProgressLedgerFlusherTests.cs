using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Runs;
using AgentSmith.Tests.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

// p0356: the durable ledger is written MID-RUN — every accepted update_progress
// replace publishes a RunStoryRecordedEvent (Note included), so a reaped run
// leaves a resumable checklist behind.
public sealed class ProgressLedgerFlusherTests
{
    private static ProgressLedger Ledger(params ProgressLedgerEntry[] entries) => new(entries);

    [Fact]
    public async Task Flush_LedgerWithNote_PublishesRunStoryRecordedWithNotePersisted()
    {
        var publisher = new RecordingEventPublisher();
        var flusher = new ProgressLedgerFlusher(publisher, "run-1", NullLogger.Instance);

        flusher.Flush(Ledger(
            new ProgressLedgerEntry("1", "batch: IFoo call sites (17)", ProgressStatus.Done,
                Target: "src/A.cs", Note: "decision: wrap via factory")));
        await flusher.Completion;

        var ev = publisher.Events.OfType<RunStoryRecordedEvent>().Should().ContainSingle().Subject;
        ev.RunId.Should().Be("run-1");
        ev.AcceptanceJson.Should().BeNull("the mid-run flush must not clobber a persisted acceptance snapshot");
        var items = RunStoryJson.TryDeserialize<System.Collections.Generic.List<ProgressLedgerItemView>>(
            ev.ProgressLedgerJson);
        items.Should().ContainSingle();
        items![0].Note.Should().Be("decision: wrap via factory");
        items[0].Status.Should().Be("done");
    }

    [Fact]
    public async Task Flush_EmptyLedger_PublishesNothing()
    {
        var publisher = new RecordingEventPublisher();
        var flusher = new ProgressLedgerFlusher(publisher, "run-1", NullLogger.Instance);

        flusher.Flush(ProgressLedger.Empty);
        await flusher.Completion;

        publisher.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Flush_PublisherThrows_SwallowedAndNextFlushStillLands()
    {
        var publisher = new ThrowingThenRecordingPublisher();
        var flusher = new ProgressLedgerFlusher(publisher, "run-1", NullLogger.Instance);

        flusher.Flush(Ledger(new ProgressLedgerEntry("1", "a", ProgressStatus.Done)));
        flusher.Flush(Ledger(new ProgressLedgerEntry("1", "a", ProgressStatus.Done),
            new ProgressLedgerEntry("2", "b", ProgressStatus.Pending)));
        await flusher.Completion;

        publisher.Recorded.Should().HaveCount(1, "the first publish threw; the second landed");
    }

    [Fact]
    public async Task UpdateProgress_AcceptedReplace_InvokesFlushCallback()
    {
        ProgressLedger? flushed = null;
        var host = new ProgressLedgerToolHost(onReplaced: l => flushed = l);

        host.UpdateProgress(new[] { new ProgressUpdateItem("1", "a", "in_progress") });

        flushed.Should().NotBeNull();
        flushed!.Entries.Should().ContainSingle(e => e.Id == "1");
        await Task.CompletedTask;
    }

    [Fact]
    public void UpdateProgress_RejectedReplace_DoesNotFlush()
    {
        var flushes = 0;
        var host = new ProgressLedgerToolHost(onReplaced: _ => flushes++);

        host.UpdateProgress(new[]
        {
            new ProgressUpdateItem("1", "a", "in_progress"),
            new ProgressUpdateItem("2", "b", "in_progress"),
        });

        flushes.Should().Be(0, "a rejected replace must not persist a ledger nothing accepted");
    }

    private sealed class ThrowingThenRecordingPublisher : IEventPublisher
    {
        private int _calls;
        public System.Collections.Generic.List<RunEvent> Recorded { get; } = new();

        public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 1) throw new InvalidOperationException("bus down");
            lock (Recorded) Recorded.Add(runEvent);
            return Task.CompletedTask;
        }
    }
}
