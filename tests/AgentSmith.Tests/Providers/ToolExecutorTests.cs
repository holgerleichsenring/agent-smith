using System.Text.Json.Nodes;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers;

public class ToolExecutorTests : IDisposable
{
    private readonly string _repoPath;
    private readonly ToolExecutor _sut;

    public ToolExecutorTests()
    {
        _repoPath = Path.Combine(Path.GetTempPath(), "agentsmith-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_repoPath);
        _sut = new ToolExecutor(_repoPath, NullLogger.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoPath))
            Directory.Delete(_repoPath, recursive: true);
    }

    [Fact]
    public async Task ReadFile_ExistingFile_ReturnsContent()
    {
        File.WriteAllText(Path.Combine(_repoPath, "test.txt"), "hello world");
        var input = new JsonObject { ["path"] = "test.txt" };

        var result = await _sut.ExecuteAsync("read_file", input);

        result.Should().Be("hello world");
    }

    [Fact]
    public async Task ReadFile_NonExistentFile_ReturnsError()
    {
        var input = new JsonObject { ["path"] = "missing.txt" };

        var result = await _sut.ExecuteAsync("read_file", input);

        result.Should().StartWith("Error:");
    }

    [Fact]
    public async Task ReadFile_PathTraversal_ReturnsError()
    {
        var input = new JsonObject { ["path"] = "../etc/passwd" };

        var result = await _sut.ExecuteAsync("read_file", input);

        result.Should().StartWith("Error:");
    }

    [Fact]
    public async Task ReadFile_AbsolutePath_ReturnsError()
    {
        var input = new JsonObject { ["path"] = "/etc/passwd" };

        var result = await _sut.ExecuteAsync("read_file", input);

        result.Should().StartWith("Error:");
    }

    [Fact]
    public async Task WriteFile_NewFile_CreatesFileAndTracksChange()
    {
        var input = new JsonObject
        {
            ["path"] = "new-file.cs",
            ["content"] = "public class Foo { }"
        };

        var result = await _sut.ExecuteAsync("write_file", input);

        result.Should().Contain("new-file.cs");
        File.Exists(Path.Combine(_repoPath, "new-file.cs")).Should().BeTrue();
        File.ReadAllText(Path.Combine(_repoPath, "new-file.cs")).Should().Be("public class Foo { }");

        var changes = _sut.GetChanges();
        changes.Should().HaveCount(1);
        changes[0].ChangeType.Should().Be("Create");
        changes[0].Path.Value.Should().Be("new-file.cs");
    }

    [Fact]
    public async Task WriteFile_ExistingFile_ModifiesAndTracksAsModify()
    {
        File.WriteAllText(Path.Combine(_repoPath, "existing.cs"), "old content");

        var input = new JsonObject
        {
            ["path"] = "existing.cs",
            ["content"] = "new content"
        };

        var result = await _sut.ExecuteAsync("write_file", input);

        File.ReadAllText(Path.Combine(_repoPath, "existing.cs")).Should().Be("new content");

        var changes = _sut.GetChanges();
        changes.Should().HaveCount(1);
        changes[0].ChangeType.Should().Be("Modify");
    }

    [Fact]
    public async Task WriteFile_NestedPath_CreatesDirectories()
    {
        var input = new JsonObject
        {
            ["path"] = "src/Models/User.cs",
            ["content"] = "public class User { }"
        };

        var result = await _sut.ExecuteAsync("write_file", input);

        File.Exists(Path.Combine(_repoPath, "src", "Models", "User.cs")).Should().BeTrue();
    }

    [Fact]
    public async Task ListFiles_ReturnsRelativePaths()
    {
        Directory.CreateDirectory(Path.Combine(_repoPath, "src"));
        File.WriteAllText(Path.Combine(_repoPath, "src", "Program.cs"), "");
        File.WriteAllText(Path.Combine(_repoPath, "README.md"), "");

        var input = new JsonObject { ["path"] = "" };

        var result = await _sut.ExecuteAsync("list_files", input);

        result.Should().Contain("README.md");
        result.Should().Contain(Path.Combine("src", "Program.cs"));
    }

    [Fact]
    public async Task ListFiles_ExcludesGitDirectory()
    {
        Directory.CreateDirectory(Path.Combine(_repoPath, ".git", "objects"));
        File.WriteAllText(Path.Combine(_repoPath, ".git", "config"), "");
        File.WriteAllText(Path.Combine(_repoPath, "README.md"), "");

        var input = new JsonObject { ["path"] = "" };

        var result = await _sut.ExecuteAsync("list_files", input);

        result.Should().Contain("README.md");
        result.Should().NotContain(".git");
    }

    [Fact]
    public async Task RunCommand_EchoCommand_ReturnsOutput()
    {
        var input = new JsonObject { ["command"] = "echo hello" };

        var result = await _sut.ExecuteAsync("run_command", input);

        result.Should().Contain("hello");
        result.Should().Contain("Exit code: 0");
    }

    [Fact]
    public async Task RunCommand_FailingCommand_ReturnsNonZeroExitCode()
    {
        var input = new JsonObject { ["command"] = "exit 1" };

        var result = await _sut.ExecuteAsync("run_command", input);

        result.Should().Contain("Exit code: 1");
    }

    [Fact]
    public async Task UnknownTool_ReturnsError()
    {
        var input = new JsonObject();

        var result = await _sut.ExecuteAsync("unknown_tool", input);

        result.Should().Contain("Unknown tool");
    }
}
