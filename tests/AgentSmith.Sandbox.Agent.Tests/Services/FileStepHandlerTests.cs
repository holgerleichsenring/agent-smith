using System.Text;
using System.Text.Json;
using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public class FileStepHandlerTests : IDisposable
{
    private readonly string _tempDir;

    public FileStepHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"fsh-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public async Task ReadFile_HappyPath_ReturnsContentInOutputContent()
    {
        var path = Path.Combine(_tempDir, "hello.txt");
        await File.WriteAllTextAsync(path, "hello world");
        var step = MakeStep(StepKind.ReadFile, path);

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.OutputContent.Should().Be("hello world");
    }

    [Fact]
    public async Task ReadFile_MissingFile_ReturnsExitCodeOne()
    {
        var step = MakeStep(StepKind.ReadFile, Path.Combine(_tempDir, "missing.txt"));
        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        result.ExitCode.Should().Be(1);
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ReadFile_OverSizeLimit_ReturnsExitCodeOneWithLimitMessage()
    {
        var path = Path.Combine(_tempDir, "big.txt");
        await File.WriteAllBytesAsync(path, new byte[SizeLimits.ReadFileMaxBytes + 1]);
        var step = MakeStep(StepKind.ReadFile, path);

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        result.ExitCode.Should().Be(1);
        result.ErrorMessage.Should().Contain("1 MB");
    }

    [Fact]
    public async Task ReadFile_BinaryContent_RejectsWithUtf8Message()
    {
        var path = Path.Combine(_tempDir, "binary.png");
        await File.WriteAllBytesAsync(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0xFF, 0xFE });
        var step = MakeStep(StepKind.ReadFile, path);

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        result.ExitCode.Should().Be(1);
        result.ErrorMessage.Should().Contain("UTF-8");
    }

    [Fact]
    public async Task WriteFile_CreatesFileWithUtf8Content()
    {
        var path = Path.Combine(_tempDir, "write.txt");
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            Path: path, Content: "ümläut");

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        File.ReadAllText(path, Encoding.UTF8).Should().Be("ümläut");
    }

    [Fact]
    public async Task WriteFile_OverwritesExistingFileAtomically()
    {
        var path = Path.Combine(_tempDir, "existing.txt");
        await File.WriteAllTextAsync(path, "old");
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            Path: path, Content: "new");

        await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        File.ReadAllText(path).Should().Be("new");
        Directory.GetFiles(_tempDir, "*.tmp.*").Should().BeEmpty("temp file must be moved, not left behind");
    }

    [Fact]
    public async Task WriteFile_OverSizeLimit_ReturnsExitCodeOneWithoutWriting()
    {
        var path = Path.Combine(_tempDir, "huge.txt");
        var oversized = new string('a', (int)SizeLimits.WriteFileMaxBytes + 10);
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            Path: path, Content: oversized);

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        result.ExitCode.Should().Be(1);
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task ListFiles_MaxDepthOne_ReturnsTopLevelEntriesOnly()
    {
        var subdir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subdir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "top.txt"), "");
        await File.WriteAllTextAsync(Path.Combine(subdir, "nested.txt"), "");
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ListFiles,
            Path: _tempDir, MaxDepth: 1);

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        var entries = JsonSerializer.Deserialize<string[]>(result.OutputContent!)!;
        entries.Should().Contain(e => e.EndsWith("top.txt"));
        entries.Should().Contain(e => e.EndsWith("sub"));
        entries.Should().NotContain(e => e.EndsWith("nested.txt"));
    }

    [Fact]
    public async Task ListFiles_MaxDepthTwo_IncludesNestedEntries()
    {
        var subdir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subdir);
        await File.WriteAllTextAsync(Path.Combine(subdir, "nested.txt"), "");
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ListFiles,
            Path: _tempDir, MaxDepth: 2);

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        var entries = JsonSerializer.Deserialize<string[]>(result.OutputContent!)!;
        entries.Should().Contain(e => e.EndsWith("nested.txt"));
    }

    [Fact]
    public async Task ListFiles_OverEntryLimit_TruncatesAndEmitsStderrEvent()
    {
        for (var i = 0; i < SizeLimits.ListFilesMaxEntries + 50; i++)
            await File.WriteAllTextAsync(Path.Combine(_tempDir, $"f{i}.txt"), "");
        var emitted = new List<StepEvent>();
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ListFiles,
            Path: _tempDir, MaxDepth: 1);

        var result = await NewHandler().HandleAsync(step,
            batch => { emitted.AddRange(batch); return Task.CompletedTask; },
            CancellationToken.None);

        var entries = JsonSerializer.Deserialize<string[]>(result.OutputContent!)!;
        entries.Length.Should().Be(SizeLimits.ListFilesMaxEntries);
        emitted.Should().Contain(e => e.Kind == StepEventKind.Stderr && e.Line.Contains("truncated"));
    }

    private static FileStepHandler NewHandler() =>
        new(NullLogger<FileStepHandler>.Instance);

    private static Step MakeStep(StepKind kind, string path) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), kind, Path: path);

    private static Task NoEvents(IReadOnlyList<StepEvent> _) => Task.CompletedTask;
}
