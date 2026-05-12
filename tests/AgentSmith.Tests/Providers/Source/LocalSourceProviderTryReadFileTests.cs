using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0135: LocalSourceProvider.TryReadFileAsync — reads files relative to basePath,
/// returns null when the file is missing, propagates real I/O errors so callers
/// can distinguish "no context.yaml" from "filesystem is broken".
/// </summary>
public sealed class LocalSourceProviderTryReadFileTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "agentsmith-local-test-" + Guid.NewGuid().ToString("N")[..8]);

    public LocalSourceProviderTryReadFileTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task TryReadFileAsync_FileExists_ReturnsContent()
    {
        var nested = Path.Combine(_tempDir, ".agentsmith");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(nested, "context.yaml"), "stack:\n  lang: C#\n");
        var sut = new LocalSourceProvider(_tempDir);

        var result = await sut.TryReadFileAsync(".agentsmith/context.yaml", CancellationToken.None);

        result.Should().Be("stack:\n  lang: C#\n");
    }

    [Fact]
    public async Task TryReadFileAsync_FileMissing_ReturnsNull()
    {
        var sut = new LocalSourceProvider(_tempDir);

        var result = await sut.TryReadFileAsync(".agentsmith/context.yaml", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryReadFileAsync_BasePathMissing_ReturnsNull()
    {
        // No file under a non-existent base path is the same shape as "no file" —
        // File.Exists returns false either way, so we get null (not a thrown
        // DirectoryNotFoundException). Documenting this so a future refactor that
        // changes the existence-check to a try/catch doesn't accidentally change
        // the shape.
        var sut = new LocalSourceProvider(Path.Combine(_tempDir, "does-not-exist"));

        var result = await sut.TryReadFileAsync(".agentsmith/context.yaml", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryReadFileAsync_CanceledBeforeRead_Throws()
    {
        // Real propagation: a cancellation during the read must reach the
        // caller — the provider must not swallow it as "file not present".
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "file.yaml"), "stack:\n");
        var sut = new LocalSourceProvider(_tempDir);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await sut.TryReadFileAsync("file.yaml", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }
}
