using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// p0158e spec-driven multi-sandbox routing. The host parses the first path
/// segment as the repo name, dispatches to that repo's sandbox with the bare
/// (prefix-stripped) path, and requires a `repo` arg on run_command for
/// multi-repo runs. Single-repo (back-compat single-sandbox ctor) behaviour
/// preserved.
/// </summary>
public sealed class FilesystemToolHostMultiRepoTests
{
    [Fact]
    public async Task ReadFile_MultiRepo_StripsRepoPrefix_DispatchesToRightSandbox()
    {
        var harness = new Harness().WithRepo("server").WithRepo("client");

        await harness.Host.ReadFile("server/src/Foo.cs");

        harness.GetSandbox("server").Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        harness.GetSandbox("client").Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReadFile_MultiRepo_UnknownPrefix_ReturnsErrorWithKnownRepos()
    {
        var harness = new Harness().WithRepo("server").WithRepo("client");

        var act = async () => await harness.Host.ReadFile("bogus/src/Foo.cs");

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("bogus");
        ex.Which.Message.Should().Contain("server");
        ex.Which.Message.Should().Contain("client");
    }

    [Fact]
    public async Task RunCommand_MultiRepo_RequiresRepoArg()
    {
        var harness = new Harness().WithRepo("server").WithRepo("client");

        var result = await harness.Host.RunCommand("dotnet test");

        result.Should().StartWith("Error");
        result.Should().Contain("repo");
        result.Should().Contain("required");
    }

    [Fact]
    public async Task RunCommand_MultiRepo_WithRepoArg_DispatchesToThatSandbox()
    {
        var harness = new Harness().WithRepo("server").WithRepo("client");

        await harness.Host.RunCommand("dotnet test", repo: "server");

        harness.GetSandbox("server").Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        harness.GetSandbox("client").Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunCommand_SingleRepo_NoRepoArg_DispatchesToTheOne()
    {
        var sandboxMock = new Mock<ISandbox>();
        sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));
        var host = new FilesystemToolHost(sandboxMock.Object);

        var result = await host.RunCommand("ls");

        result.Should().NotStartWith("Error");
        sandboxMock.Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Monorepo_N1_ToolPath_NoPrefix_StillWorks()
    {
        var sandboxMock = new Mock<ISandbox>();
        sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));
        var host = new FilesystemToolHost(sandboxMock.Object);

        await host.ReadFile("src/Foo.cs");

        sandboxMock.Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private sealed class Harness
    {
        private readonly Dictionary<string, Mock<ISandbox>> _sandboxes = new(StringComparer.Ordinal);
        private string _defaultRepo = string.Empty;
        public FilesystemToolHost Host => new(
            _sandboxes.ToDictionary(kv => kv.Key, kv => kv.Value.Object, StringComparer.Ordinal),
            _defaultRepo);

        public Harness WithRepo(string name)
        {
            if (_defaultRepo == string.Empty) _defaultRepo = name;
            var sandboxMock = new Mock<ISandbox>();
            sandboxMock.Setup(s => s.RunStepAsync(
                    It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
                .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                    Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));
            _sandboxes[name] = sandboxMock;
            return this;
        }

        public Mock<ISandbox> GetSandbox(string name) => _sandboxes[name];
    }
}
