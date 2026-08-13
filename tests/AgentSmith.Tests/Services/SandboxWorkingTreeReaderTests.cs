using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

// p0411: the changed paths are read by the framework once per re-engagement pass,
// so the master is told them instead of re-deriving them from git itself.
public sealed class SandboxWorkingTreeReaderTests
{
    private static SandboxWorkingTreeReader Reader() =>
        new(NullLogger<SandboxWorkingTreeReader>.Instance);

    [Fact]
    public async Task ChangedPaths_SingleRepo_ReturnsTheWorkingTreePaths()
    {
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal)
        {
            ["server"] = new ScriptedSandbox(" M src/Api.cs\n?? src/New.cs\n"),
        };

        var paths = await Reader().ChangedPathsAsync(sandboxes, null, CancellationToken.None);

        paths.Should().BeEquivalentTo(["src/Api.cs", "src/New.cs"]);
    }

    [Fact]
    public async Task ChangedPaths_MultiRepo_PrefixesEachPathWithItsRepoName()
    {
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal)
        {
            ["server"] = new ScriptedSandbox(" M Program.cs\n"),
            ["client"] = new ScriptedSandbox(" M app.ts\n"),
        };
        var keyToRepo = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["server"] = "server", ["client"] = "client",
        };

        var paths = await Reader().ChangedPathsAsync(sandboxes, keyToRepo, CancellationToken.None);

        paths.Should().BeEquivalentTo(["client/app.ts", "server/Program.cs"]);
    }

    [Fact]
    public async Task ChangedPaths_SandboxCannotRunGit_ContributesNothing()
    {
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal)
        {
            ["gone"] = new ScriptedSandbox(string.Empty, exitCode: -1),
        };

        (await Reader().ChangedPathsAsync(sandboxes, null, CancellationToken.None))
            .Should().BeEmpty();
    }

    [Fact]
    public void ParsePorcelain_Rename_KeepsTheNewPath()
    {
        SandboxWorkingTreeReader.ParsePorcelain("R  old/Name.cs -> new/Name.cs\n")
            .Should().BeEquivalentTo(["new/Name.cs"]);
    }

    private sealed class ScriptedSandbox(string porcelain, int exitCode = 0) : ISandbox
    {
        public string JobId => "worktree-test";

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: porcelain));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
