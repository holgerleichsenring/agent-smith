using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Services;
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

        var runTask = sut.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await cts.CancelAsync();

        try { await sut.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        _enqueuer.EnqueuedFiles.Should().NotBeEmpty();
        _enqueuer.EnqueuedFiles[0].Should().Contain("test.pdf");
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
        var runTask = sut.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(1));
        await cts.CancelAsync();

        try { await sut.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        _enqueuer.EnqueuedFiles.Should().Contain(f => f.Contains("orphan.pdf"));
    }

    [Fact]
    public async Task Service_SkipsMetaJsonFiles()
    {
        var options = CreateOptions();
        Directory.CreateDirectory(options.InboxPath);

        await File.WriteAllTextAsync(Path.Combine(options.InboxPath, "doc.pdf.meta.json"), "{}");

        var sut = new InboxPollingService(
            _enqueuer, options, NullLogger<InboxPollingService>.Instance);

        using var cts = new CancellationTokenSource();
        var runTask = sut.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await cts.CancelAsync();

        try { await sut.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        _enqueuer.EnqueuedFiles.Should().BeEmpty();
    }

    private InboxPollingOptions CreateOptions() => new()
    {
        InboxPath = Path.Combine(_baseDir, "inbox"),
        ProcessingPath = Path.Combine(_baseDir, "processing"),
        OutboxPath = Path.Combine(_baseDir, "outbox"),
        ArchivePath = Path.Combine(_baseDir, "archive"),
        PollIntervalSeconds = 1,
    };

    private sealed class FakeJobEnqueuer : IInboxJobEnqueuer
    {
        public List<string> EnqueuedFiles { get; } = [];
        public List<string?> EnqueuedMetadata { get; } = [];

        public Task EnqueueAsync(string filePath, string? metadata, CancellationToken cancellationToken)
        {
            EnqueuedFiles.Add(filePath);
            EnqueuedMetadata.Add(metadata);
            return Task.CompletedTask;
        }
    }
}
