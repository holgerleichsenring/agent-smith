using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0197: real `git` via InProcessSandbox. Git ships with macOS / every CI
/// runner, so no skip is strictly needed — but we keep the guard symmetric
/// with the dotnet/npm classes for pre-commit on dev machines without git.
/// </summary>
public sealed class GitSandboxExecutionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task GitClone_FromPublicHttp_Succeeds()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;

        await using var fixture = FixtureWorkdir.CreateEmpty();
        await using var sandbox = NewSandbox(fixture.Path);

        // octocat/Hello-World is GitHub's canonical "always-stable" sample
        // repo. ~4 commits, depth 1 keeps it fast (<2s typical).
        var result = await sandbox.RunStepAsync(
            BuildRunStep(sandbox.WorkDir, "git",
                new[] { "clone", "--depth", "1", "https://github.com/octocat/Hello-World.git", "cloned" },
                timeoutSec: 60),
            progress: null, CancellationToken.None);

        output.WriteLine($"exit={result.ExitCode} err={result.ErrorMessage}");
        result.ExitCode.Should().Be(0, "git clone of public Hello-World must succeed");
        Directory.Exists(Path.Combine(fixture.Path, "cloned", ".git"))
            .Should().BeTrue("cloned repo must contain a .git directory");
    }

    [Fact]
    public async Task GitInitCommit_LocalWorkflow_Succeeds()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;

        await using var fixture = FixtureWorkdir.CreateEmpty();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "README.md"), "# test\n");
        await using var sandbox = NewSandbox(fixture.Path);

        await Run(sandbox, "git", new[] { "init", "-b", "main" });
        await Run(sandbox, "git", new[] { "config", "user.email", "test@example.com" });
        await Run(sandbox, "git", new[] { "config", "user.name", "test" });
        await Run(sandbox, "git", new[] { "add", "README.md" });
        var commit = await sandbox.RunStepAsync(
            BuildRunStep(sandbox.WorkDir, "git", new[] { "commit", "-m", "test" }, 30),
            progress: null, CancellationToken.None);

        output.WriteLine($"commit exit={commit.ExitCode} err={commit.ErrorMessage}");
        commit.ExitCode.Should().Be(0, "git commit on staged file must succeed");
    }

    private static async Task Run(InProcessSandbox sandbox, string cmd, string[] args)
    {
        var r = await sandbox.RunStepAsync(
            BuildRunStep(sandbox.WorkDir, cmd, args, 30),
            progress: null, CancellationToken.None);
        r.ExitCode.Should().Be(0, $"{cmd} {string.Join(' ', args)} must succeed: {r.ErrorMessage}");
    }

    private static InProcessSandbox NewSandbox(string workdir) =>
        new(jobId: "test", workDir: workdir, ownsWorkDir: false, NullLogger.Instance);

    private static Step BuildRunStep(string cwd, string cmd, string[] args, int timeoutSec) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: cmd, Args: args, WorkingDirectory: cwd, TimeoutSeconds: timeoutSec);
}
