using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-25-9992c: a sandbox's root is the first one its wrapper chain declares.</summary>
public sealed class SandboxWorkDirectoryExtensionsTests
{
    [Fact]
    public void WorkDirectory_UndeclaredSandbox_FallsBackToTheWorkspaceRoot() =>
        new StepRecordingSandbox().WorkDirectory().Should().Be("/work");

    [Fact]
    public void WorkDirectory_WrapperWithoutRoot_AsksTheSandboxItWraps()
    {
        var wrapped = new Wrapper(new RepoSandbox(new StepRecordingSandbox(), "api"));

        wrapped.WorkDirectory().Should().Be("/work/api");
    }

    [Fact]
    public void WorkDirectory_OutermostDeclaredRoot_Wins()
    {
        var chain = new RepoSandbox(new Wrapper(new RepoSandbox(new StepRecordingSandbox(), "inner")), "outer");

        chain.WorkDirectory().Should().Be("/work/outer");
    }

    [Fact]
    public void WorkDirectory_ProjectorOverABackend_ServesTheBackendRoot() =>
        new SandboxEventProjector(new Declared("/somewhere"), Moq.Mock.Of<AgentSmith.Contracts.Events.IEventPublisher>(),
                Moq.Mock.Of<AgentSmith.Contracts.Events.IRunContextAccessor>(), "key")
            .WorkDirectory().Should().Be("/somewhere");

    [Theory]
    [InlineData("src/a.cs", "/work/api/src/a.cs")]
    [InlineData("/src/a.cs", "/work/api/src/a.cs")]
    public void PathUnderRoot_Relative_IsJoinedOnce(string relative, string expected) =>
        new RepoSandbox(new StepRecordingSandbox(), "api").PathUnderRoot(relative).Should().Be(expected);

    private sealed class Wrapper(ISandbox inner) : ISandbox, ISandboxWrapper
    {
        public ISandbox Inner => inner;
        public string JobId => inner.JobId;
        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct) =>
            inner.RunStepAsync(step, progress, ct);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class Declared(string root) : ISandbox, ISandboxWorkDirectory
    {
        public string WorkDirectory => root;
        public string JobId => "declared";
        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct) =>
            throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
