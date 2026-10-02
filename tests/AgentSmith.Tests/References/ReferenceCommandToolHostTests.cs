using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-02-075dc: run_in_reference runs a shell in the named upload's own container, with
/// run_command's timeout clamp and output — and only on a design turn that has an upload, where
/// sandboxes are containers.
/// </summary>
public sealed class ReferenceCommandToolHostTests
{
    private readonly InMemoryFileSandbox _upload = new();

    [Fact]
    public async Task ReferenceCommandToolHost_RunsInTheNamedReference_ThroughTheShell()
    {
        var host = Host(stepCap: null);

        var output = await host.RunInReference("reference:app", "ls app", null, CancellationToken.None);

        _upload.Shells.Should().Equal("ls app");
        output.Should().Contain("exit_code: 0");
    }

    [Fact]
    public async Task ReferenceCommandToolHost_TimeoutClampedToTheStepCap()
    {
        var recorder = new StepRecorder();
        var host = new ReferenceCommandToolHost(
            new Dictionary<string, ISandbox> { ["reference:app"] = recorder }, new RunCommandTimeout(120, 300));

        await host.RunInReference("reference:app", "pip install x", 9000, CancellationToken.None);

        recorder.Last!.TimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public async Task ReferenceCommandToolHost_RepoOrTemplateAddress_AnswersErrorListingReferences()
    {
        var output = await Host(stepCap: null).RunInReference("server", "ls", null, CancellationToken.None);

        output.Should().StartWith("Error:").And.Contain("reference:app");
        _upload.Shells.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ReferenceDesignTools_For_NeedsAContainerRuntimeAndAnUpload(bool containers, bool upload, bool expected)
    {
        var pipeline = new PipelineContext();
        var map = new Dictionary<string, ISandbox> { ["server"] = new InMemoryFileSandbox() };
        if (upload) map["reference:app"] = _upload;
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, map);

        var tools = new ReferenceDesignTools(new SandboxContainerRuntime(containers)).For(pipeline);

        tools.Select(t => t.Name).Contains("run_in_reference").Should().Be(expected);
    }

    private ReferenceCommandToolHost Host(int? stepCap) =>
        new(new Dictionary<string, ISandbox> { ["reference:app"] = _upload }, new RunCommandTimeout(null, stepCap));

    private sealed class StepRecorder : ISandbox
    {
        public Step? Last { get; private set; }

        public string JobId => "recorder";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            Last = step;
            return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0, null, null));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
