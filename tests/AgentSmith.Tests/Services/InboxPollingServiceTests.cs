using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class InboxPollingServiceTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), $"ast-inbox-{Guid.NewGuid():N}");
    private readonly FakeJobEnqueuer _enqueuer = new();

    public InboxPollingServiceTests() => Directory.CreateDirectory(_baseDir);

    public void Dispose()
    {
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }

    [Fact]
    public async Task Service_DetectsNewFiles_EnqueuesJob()
    {
        var options = CreateOptions();
        Directory.CreateDirectory(options.InboxPath);

        var sut = new InboxPollingService(
            _enqueuer, options, NullLogger<InboxPollingService>.Instance);

        using var cts = new CancellationTokenSource();

        var testFile = Path.Combine(options.InboxPath, "test.pdf");
        await File.WriteAllTextAsync(testFile, "content");

        // 2026-09-22-3f7c: the poll runs on its own timer, so the test waits for the ENQUEUE
        // rather than for a stretch of clock in which one was likely. Two seconds was two poll
        // intervals on the machine that wrote it and rather fewer on a loaded runner.
        await sut.StartAsync(cts.Token);
        var enqueued = await TestWaits.ReachedAsync(() => _enqueuer.Files().Count > 0);
        await cts.CancelAsync();

        try { await sut.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        enqueued.Should().BeTrue("the poll must pick the new file up");
        _enqueuer.Files()[0].Should().Contain("test.pdf");
    }

    [Fact]
    public async Task Service_RecoverOrphanedFiles_ReEnqueues()
    {
        var options = CreateOptions();
        Directory.CreateDirectory(options.ProcessingPath);

        var orphanFile = Path.Combine(options.ProcessingPath, "orphan.pdf");
        await File.WriteAllTextAsync(orphanFile, "orphaned");

        var sut = new InboxPollingService(
            _enqueuer, options, NullLogger<InboxPollingService>.Instance);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        var recovered = await TestWaits.ReachedAsync(
            () => _enqueuer.Files().Any(f => f.Contains("orphan.pdf")));
        await cts.CancelAsync();

        try { await sut.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        recovered.Should().BeTrue("a file left in processing is re-enqueued at start-up");
    }

    [Fact]
    public async Task Service_SkipsMetaJsonFiles()
    {
        var options = CreateOptions();
        Directory.CreateDirectory(options.InboxPath);

        await File.WriteAllTextAsync(Path.Combine(options.InboxPath, "doc.pdf.meta.json"), "{}");
        // A decoy in the same sweep, so "the sidecar was skipped" is proven by a poll that
        // demonstrably ran rather than by a window in which one might not have.
        await File.WriteAllTextAsync(Path.Combine(options.InboxPath, "decoy.pdf"), "content");

        var sut = new InboxPollingService(
            _enqueuer, options, NullLogger<InboxPollingService>.Instance);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        var swept = await TestWaits.ReachedAsync(
            () => _enqueuer.Files().Any(f => f.Contains("decoy.pdf")));
        await cts.CancelAsync();

        try { await sut.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        swept.Should().BeTrue("the decoy proves the sweep this assertion is about actually ran");
        _enqueuer.Files().Should().NotContain(f => f.Contains("meta.json"));
    }

    private InboxPollingOptions CreateOptions() => new()
    {
        InboxPath = Path.Combine(_baseDir, "inbox"),
        ProcessingPath = Path.Combine(_baseDir, "processing"),
        OutboxPath = Path.Combine(_baseDir, "outbox"),
        ArchivePath = Path.Combine(_baseDir, "archive"),
        PollIntervalSeconds = 1,
    };

    // The service enqueues from its own poll loop while the test reads, so the list is held
    // under a lock and handed out as a snapshot — a poll must never race the producer it polls.
    private sealed class FakeJobEnqueuer : IInboxJobEnqueuer
    {
        private readonly List<string> _files = [];

        public IReadOnlyList<string> Files()
        {
            lock (_files) return [.. _files];
        }

        public Task EnqueueAsync(string filePath, string? metadata, CancellationToken cancellationToken)
        {
            lock (_files) _files.Add(filePath);
            return Task.CompletedTask;
        }
    }
}
