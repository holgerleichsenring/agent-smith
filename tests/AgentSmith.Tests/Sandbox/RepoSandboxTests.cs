using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-25-9992c: a run repository's sandbox roots what its steps leave relative at /work/&lt;repo&gt;.</summary>
public sealed class RepoSandboxTests
{
    private readonly StepRecordingSandbox _inner = new();

    private async Task<Step> SentAsync(Step step)
    {
        await new RepoSandbox(_inner, "api").RunStepAsync(step, null, CancellationToken.None);
        return _inner.Steps.Single();
    }

    private static Step Run(string? workingDirectory) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git", Args: ["status"],
            WorkingDirectory: workingDirectory);

    private static Step Read(string path) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile, Path: path);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("./")]
    public async Task RunStepAsync_NoOrDotWorkingDirectory_RunsAtTheRepositoryRoot(string? workingDirectory) =>
        (await SentAsync(Run(workingDirectory))).WorkingDirectory.Should().Be("/work/api");

    [Theory]
    [InlineData("src", "/work/api/src")]
    [InlineData("./src/app", "/work/api/src/app")]
    [InlineData("/work", "/work")]
    [InlineData("/tmp/x", "/tmp/x")]
    public async Task RunStepAsync_WorkingDirectory_RelativeIsRootedAndAbsolutePasses(string given, string expected) =>
        (await SentAsync(Run(given))).WorkingDirectory.Should().Be(expected);

    [Theory]
    [InlineData("README.md", "/work/api/README.md")]
    [InlineData("./src/a.cs", "/work/api/src/a.cs")]
    [InlineData(".", "/work/api")]
    [InlineData("/root/.npmrc", "/root/.npmrc")]
    public async Task RunStepAsync_Path_RelativeIsRootedAndAbsolutePasses(string given, string expected) =>
        (await SentAsync(Read(given))).Path.Should().Be(expected);

    [Fact]
    public async Task RunStepAsync_RenameTo_IsRootedLikeThePath()
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteBytes,
            Path: "a.part", Content: "AA==", RenameTo: "a.bin");

        var sent = await SentAsync(step);

        sent.Path.Should().Be("/work/api/a.part");
        sent.RenameTo.Should().Be("/work/api/a.bin");
    }

    [Fact]
    public async Task RunStepAsync_RelativePathAndWorkingDirectory_PathResolvesUnderTheWorkingDirectory()
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ListFiles,
            Path: "lib", WorkingDirectory: "src");

        var sent = await SentAsync(step);

        sent.WorkingDirectory.Should().Be("/work/api/src");
        sent.Path.Should().Be("/work/api/src/lib");
    }

    [Fact]
    public async Task RunStepAsync_Args_AreNeverTouched()
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git",
            Args: ["-C", "src", "add", "."]);

        (await SentAsync(step)).Args.Should().Equal("-C", "src", "add", ".");
    }

    [Fact]
    public void WorkDirectory_IsTheRepositoryRoot_AndTheInnerSandboxIsReachable()
    {
        var sandbox = new RepoSandbox(_inner, "api");

        sandbox.WorkDirectory().Should().Be("/work/api");
        sandbox.Backend().Should().BeSameAs(_inner);
    }
}
