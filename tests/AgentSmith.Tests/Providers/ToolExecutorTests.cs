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

    [Theory]
    [InlineData("dotnet run")]
    [InlineData("dotnet run --urls=http://localhost:5001")]
    [InlineData("dotnet watch")]
    [InlineData("npm start")]
    [InlineData("npm run dev")]
    [InlineData("npm run serve")]
    [InlineData("yarn start")]
    [InlineData("yarn dev")]
    [InlineData("ng serve")]
    [InlineData("python -m http.server")]
    [InlineData("python manage.py runserver")]
    [InlineData("flask run")]
    [InlineData("uvicorn main:app")]
    [InlineData("gunicorn app:app")]
    [InlineData("docker run nginx")]
    [InlineData("docker compose up")]
    [InlineData("docker-compose up")]
    [InlineData("vite")]
    [InlineData("webpack serve")]
    public void IsBlockedCommand_BlockedCommands_ReturnsTrue(string command)
    {
        CommandRunner.IsBlockedCommand(command).Should().BeTrue();
    }

    [Theory]
    [InlineData("dotnet build")]
    [InlineData("dotnet test")]
    [InlineData("dotnet restore")]
    [InlineData("npm install")]
    [InlineData("npm test")]
    [InlineData("npm run build")]
    [InlineData("npm run test")]
    [InlineData("yarn install")]
    [InlineData("yarn test")]
    [InlineData("echo hello")]
    [InlineData("ls -la")]
    [InlineData("python -m pytest")]
    [InlineData("docker build .")]
    public void IsBlockedCommand_AllowedCommands_ReturnsFalse(string command)
    {
        CommandRunner.IsBlockedCommand(command).Should().BeFalse();
    }

    [Theory]
    [InlineData("dotnet build && dotnet run")]
    [InlineData("dotnet build; dotnet run --urls=http://localhost:5001")]
    [InlineData("nohup dotnet run &")]
    public void IsBlockedCommand_BlockedInMultiCommand_ReturnsTrue(string command)
    {
        CommandRunner.IsBlockedCommand(command).Should().BeTrue();
    }

    [Theory]
    [InlineData("dotnet run --urls=\"http://localhost:5001\" &\nsleep 2\ncurl -s http://localhost:5001/todos")]
    public void IsBlockedCommand_BlockedInMultiLineCommand_ReturnsTrue(string command)
    {
        CommandRunner.IsBlockedCommand(command).Should().BeTrue();
    }

    [Fact]
    public async Task RunCommand_BlockedCommand_ReturnsErrorWithoutExecuting()
    {
        var input = new JsonObject { ["command"] = "dotnet run" };

        var result = await _sut.ExecuteAsync("run_command", input);

        result.Should().Contain("Error: Command rejected");
        result.Should().Contain("Long-running server processes are not allowed");
    }
}
