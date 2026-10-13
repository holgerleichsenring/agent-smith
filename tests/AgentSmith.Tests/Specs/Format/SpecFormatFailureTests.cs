using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Format;

/// <summary>
/// 2026-10-13-a347a: a formatter that fails at the spec commit is recorded for its repository's code
/// PR — the declared command, the exit code and the masked tail of its output.
/// </summary>
public sealed class SpecFormatFailureTests
{
    private const string Declared = "dotnet csharpier {files}";

    [Fact]
    public async Task SpecFormatRun_Failure_IsRecordedWithItsOutput()
    {
        var sandbox = new FormatSandbox
        {
            ShellExitCode = 1,
            ShellOutput = new string('x', 2000) + "\ntoken s3cr3t-value\nUnrecognized command or argument 'src/A.cs'",
        };
        sandbox.Changed.Add("src/A.cs");
        var pipeline = FormatFixture.Pipeline(sandbox, FormatFixture.Context("api", ".", Declared));

        await FormatFixture.Run("s3cr3t-value").RunAsync(pipeline, CancellationToken.None);

        var failure = pipeline.Get<IReadOnlyList<SpecFormatFailure>>(ContextKeys.SpecFormatFailures).Should().ContainSingle().Subject;
        failure.Repository.Should().Be(FormatFixture.Key);
        failure.ContextName.Should().Be("api");
        failure.DeclaredCommand.Should().Be(Declared, "the operator corrects the command as declared, not its expansion");
        failure.ExitCode.Should().Be(1);
        failure.OutputTail.Should().HaveLength(SpecFormatFailureRecorder.OutputTailChars)
            .And.EndWith("Unrecognized command or argument 'src/A.cs'")
            .And.Contain("token ***").And.NotContain("s3cr3t-value");
    }

    [Fact]
    public async Task SpecFormatRun_SameCommandFailsAgain_ReplacesAndSuccessClearsNothing()
    {
        var sandbox = new FormatSandbox { ShellExitCode = 1, ShellOutput = "first" };
        sandbox.Changed.Add("src/A.cs");
        var pipeline = FormatFixture.Pipeline(sandbox, FormatFixture.Context("api", ".", Declared));
        var run = FormatFixture.Run();

        await run.RunAsync(pipeline, CancellationToken.None);
        sandbox.ShellExitCode = 2;
        sandbox.ShellOutput = "second";
        await run.RunAsync(pipeline, CancellationToken.None);
        sandbox.ShellExitCode = 0;
        await run.RunAsync(pipeline, CancellationToken.None);

        pipeline.Get<IReadOnlyList<SpecFormatFailure>>(ContextKeys.SpecFormatFailures)
            .Should().ContainSingle().Which.Should().Match<SpecFormatFailure>(f => f.ExitCode == 2 && f.OutputTail == "second");
    }

    [Fact]
    public async Task SpecFormatRun_FormatterCannotStart_RecordsWhatStoppedIt()
    {
        var pipeline = FormatFixture.Pipeline(new ThrowingSandbox(), FormatFixture.Context("api", ".", Declared));

        await FormatFixture.Run().RunAsync(pipeline, CancellationToken.None);

        pipeline.Get<IReadOnlyList<SpecFormatFailure>>(ContextKeys.SpecFormatFailures)
            .Should().ContainSingle().Which.Should().Match<SpecFormatFailure>(
                f => f.ExitCode == -1 && f.OutputTail == ThrowingSandbox.Message);
    }

    private sealed class ThrowingSandbox : AgentSmith.Contracts.Sandbox.ISandbox
    {
        public const string Message = "sandbox lost";
        private readonly FormatSandbox _inner = new();

        public ThrowingSandbox() => _inner.Changed.Add("src/A.cs");

        public string JobId => "throwing";

        public Task<AgentSmith.Sandbox.Wire.StepResult> RunStepAsync(
            AgentSmith.Sandbox.Wire.Step step, IProgress<AgentSmith.Sandbox.Wire.StepEvent>? progress, CancellationToken ct) =>
            FormatSandbox.IsShell(step) ? throw new InvalidOperationException(Message) : _inner.RunStepAsync(step, progress, ct);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
