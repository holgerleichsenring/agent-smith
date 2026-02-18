using System.Text.Json.Nodes;
using AgentSmith.Infrastructure.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers;

public class ToolExecutorFileTrackingTests : IDisposable
{
    private readonly string _repoPath;
    private readonly FileReadTracker _tracker;
    private readonly ToolExecutor _sut;

    public ToolExecutorFileTrackingTests()
    {
        _repoPath = Path.Combine(Path.GetTempPath(), "agentsmith-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_repoPath);
        _tracker = new FileReadTracker();
        _sut = new ToolExecutor(_repoPath, NullLogger.Instance, _tracker);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoPath))
            Directory.Delete(_repoPath, recursive: true);
    }

    [Fact]
    public async Task ReadFile_FirstRead_ReturnsFullContent()
    {
        File.WriteAllText(Path.Combine(_repoPath, "test.cs"), "public class Foo { }");
        var input = new JsonObject { ["path"] = "test.cs" };

        var result = await _sut.ExecuteAsync("read_file", input);

        result.Should().Be("public class Foo { }");
        _tracker.GetReadCount("test.cs").Should().Be(1);
    }

    [Fact]
    public async Task ReadFile_SecondRead_ReturnsShortReference()
    {
        File.WriteAllText(Path.Combine(_repoPath, "test.cs"), "public class Foo { }");
        var input = new JsonObject { ["path"] = "test.cs" };

        await _sut.ExecuteAsync("read_file", input); // first read
        var result = await _sut.ExecuteAsync("read_file", input); // second read

        result.Should().Contain("[File previously read:");
        result.Should().Contain("test.cs");
        _tracker.GetReadCount("test.cs").Should().Be(2);
    }

    [Fact]
    public async Task ReadFile_AfterWrite_ReturnsFullNewContent()
    {
        File.WriteAllText(Path.Combine(_repoPath, "test.cs"), "old content");
        var readInput = new JsonObject { ["path"] = "test.cs" };
        var writeInput = new JsonObject { ["path"] = "test.cs", ["content"] = "new content" };

        await _sut.ExecuteAsync("read_file", readInput);    // first read (count=1)
        await _sut.ExecuteAsync("write_file", writeInput);   // write invalidates (count=0)
        var result = await _sut.ExecuteAsync("read_file", readInput); // re-read after invalidation (count=1)

        result.Should().Be("new content");
        _tracker.GetReadCount("test.cs").Should().Be(1);
    }

    [Fact]
    public async Task ReadFile_WithoutTracker_AlwaysReturnsFullContent()
    {
        var executorNoTracker = new ToolExecutor(_repoPath, NullLogger.Instance);
        File.WriteAllText(Path.Combine(_repoPath, "test.cs"), "content");
        var input = new JsonObject { ["path"] = "test.cs" };

        var first = await executorNoTracker.ExecuteAsync("read_file", input);
        var second = await executorNoTracker.ExecuteAsync("read_file", input);

        first.Should().Be("content");
        second.Should().Be("content");
    }
}
