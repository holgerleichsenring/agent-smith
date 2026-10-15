using System.Text.Json;
using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public sealed class FileStepHandlerLineRangeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public FileStepHandlerLineRangeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "lines.txt");
        File.WriteAllText(_filePath, string.Join('\n', Enumerable.Range(1, 20).Select(i => $"line {i}")));
    }

    public void Dispose() { try { Directory.Delete(_tempDir, recursive: true); } catch { } }

    [Fact]
    public async Task ReadFile_WithStartLineAndLineCount_ReturnsRequestedSlice()
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile,
            Path: _filePath, StartLine: 5, LineCount: 3, WithLineNumbers: false);

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.OutputContent.Should().Be("line 5\nline 6\nline 7");
    }

    [Fact]
    public async Task ReadFile_WithLineNumbersTrue_PrefixesEachLineWithRightAlignedNumber()
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile,
            Path: _filePath, StartLine: 1, LineCount: 2, WithLineNumbers: true);

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        // 20 lines => 2-digit width.
        result.OutputContent.Should().Be(" 1\tline 1\n 2\tline 2");
    }

    [Fact]
    public async Task ReadFile_NoStartLine_ReadsFromBeginning()
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile,
            Path: _filePath, LineCount: 2, WithLineNumbers: false);

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        result.OutputContent.Should().Be("line 1\nline 2");
    }

    [Fact]
    public async Task ReadFile_NoLineCount_ReadsToEnd()
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile,
            Path: _filePath, StartLine: 19, WithLineNumbers: false);

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        result.OutputContent.Should().Be("line 19\nline 20");
    }

    [Fact]
    public async Task ListFiles_WithSizes_AndSortBySize_OrdersDescending()
    {
        File.WriteAllText(Path.Combine(_tempDir, "small.txt"), "a");
        File.WriteAllText(Path.Combine(_tempDir, "big.txt"), new string('b', 1000));
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ListFiles,
            Path: _tempDir, MaxDepth: 1, WithSizes: true, SortBy: DirectorySortBy.Size);

        var result = await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);

        var entries = JsonSerializer.Deserialize<List<JsonElement>>(result.OutputContent!)!;
        var files = entries.Where(e => !e.GetProperty("is_directory").GetBoolean()).ToList();
        files.First().GetProperty("path").GetString().Should().Contain("big.txt");
        files.First().GetProperty("size_bytes").GetInt64().Should().Be(1000);
    }

    [Fact]
    public async Task ReadFile_NumberedFileEndingInNewline_ShowsExactlyItsLines()
    {
        var result = await ReadFixtureAsync("a\nb\n", withLineNumbers: true);

        result.OutputContent.Should().Be("1\ta\n2\tb");
    }

    [Fact]
    public async Task ReadFile_NumberedEmptyFile_ShowsNoLine()
    {
        var result = await ReadFixtureAsync("", withLineNumbers: true);

        result.ExitCode.Should().Be(0);
        result.OutputContent.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadFile_NumberedFileWithoutTrailingNewline_IsUnchanged()
    {
        var result = await ReadFixtureAsync("a\nb", withLineNumbers: true);

        result.OutputContent.Should().Be("1\ta\n2\tb");
    }

    [Fact]
    public async Task ReadFile_UnnumberedWholeFile_KeepsTheTrailingNewline()
    {
        var result = await ReadFixtureAsync("a\nb\n", withLineNumbers: false);

        result.OutputContent.Should().Be("a\nb\n");
    }

    private async Task<StepResult> ReadFixtureAsync(string content, bool withLineNumbers)
    {
        var path = Path.Combine(_tempDir, "fixture.txt");
        File.WriteAllText(path, content);
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile,
            Path: path, WithLineNumbers: withLineNumbers);
        return await NewHandler().HandleAsync(step, NoEvents, CancellationToken.None);
    }

    private static FileStepHandler NewHandler() => new(NullLogger<FileStepHandler>.Instance);
    private static Task NoEvents(IReadOnlyList<StepEvent> _) => Task.CompletedTask;
}
