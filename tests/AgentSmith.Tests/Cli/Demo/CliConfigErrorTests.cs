using AgentSmith.Cli.Commands;
using AgentSmith.Cli.Services.Demo;
using FluentAssertions;
using System.CommandLine.Parsing;

namespace AgentSmith.Tests.Cli.Demo;

/// <summary>2026-11-02-2b26a: a missing config ends a verb with its message and exit 1, never a stack trace; an
/// explicit --config to a missing file is never swapped for a generated demo config.</summary>
[Collection("ConsoleOut")]
public sealed class CliConfigErrorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentsmith-2b26a-{Guid.NewGuid():N}");

    [Fact]
    public async Task MissingConfig_PrintsMessageWithoutStack_ExitsOne()
    {
        var (exit, stderr) = await RunAsync("doctor", "--config", Path.Combine(_root, "nope.yml"));

        exit.Should().Be(1);
        stderr.Should().Contain("Configuration file not found").And.Contain("Looked in:");
        stderr.Should().NotContain("   at ", "a configuration problem is not a defect report");
    }

    [Fact]
    public async Task ExplicitMissingConfig_DoesNotGenerate()
    {
        var workspace = Path.Combine(_root, "ws");

        var (exit, _) = await RunAsync("demo", "--config", Path.Combine(_root, "nope.yml"), "--workspace", workspace);

        exit.Should().Be(1);
        File.Exists(DemoConfigFile.PathFor(workspace)).Should().BeFalse("a typo in --config must fail, not run on a demo config");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static async Task<(int Exit, string Stderr)> RunAsync(params string[] args)
    {
        var original = Console.Error;
        var stderr = new StringWriter();
        Console.SetError(stderr);
        try { return (await CliRootCommand.BuildParser().InvokeAsync(args), stderr.ToString()); }
        finally { Console.SetError(original); }
    }
}
