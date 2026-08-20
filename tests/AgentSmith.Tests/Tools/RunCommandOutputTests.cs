using System.Text;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// p0491: a live run parked asking the operator to provision a checkout that was there all
/// along — its sandbox "returned no output for pwd, ls, project discovery and mediator
/// inventory". Those commands had exited 0 with 27,355 / 2,591 / 28,888 characters in their
/// result bodies and NOTHING streamed: run_command built the model's text from the live feed,
/// so an event drain that fell behind turned a full inventory into an empty stdout.
/// <para>
/// The fixture is therefore sized like the real one. A short synthetic string would pass at
/// any budget and would not have caught this.
/// </para>
/// </summary>
public sealed class RunCommandOutputTests
{
    [Fact]
    public async Task RunAsync_BodyArrivesWithNoProgressEvents_TheInventoryStillReachesTheModel()
    {
        var inventory = SampleInventory();
        inventory.Length.Should().BeGreaterThan(27_000, "the live loss was an inventory this big");
        var sut = new SandboxStepRunner(new FakeSandbox(body: inventory));

        var text = await sut.RunAsync("find . -name '*.csproj'", null, CancellationToken.None);

        text.Should().Contain("exit_code: 0").And.Contain("truncated: false");
        text.Should().Contain("--- projects ---", "the head of what the command printed");
        text.Should().Contain("Sample.AccessPortal.Server.Module199", "and the far end of it");
        text.Length.Should().BeGreaterThan(27_000);
    }

    [Fact]
    public async Task RunAsync_StreamAndBodyBothPresent_TheBodyIsWhatTheModelReads()
    {
        var sut = new SandboxStepRunner(new FakeSandbox(
            body: "the whole listing\n", streamed: [Line(StepEventKind.Stdout, "the first line only")]));

        var text = await sut.RunAsync("ls -la", null, CancellationToken.None);

        text.Should().Contain("the whole listing");
        text.Should().NotContain("the first line only",
            "the stream is a best-effort feed; the body is what the command produced");
    }

    [Fact]
    public async Task RunAsync_BodyIsNull_FallsBackToTheStreamedOutput()
    {
        // A sandbox agent image predating p0258 leaves the body null on run steps.
        var sut = new SandboxStepRunner(new FakeSandbox(
            body: null, streamed: [Line(StepEventKind.Stdout, "from an older agent")]));

        var text = await sut.RunAsync("pwd", null, CancellationToken.None);

        text.Should().Contain("from an older agent");
    }

    [Fact]
    public async Task RunAsync_BodyExceedsTheBuffer_IsCutAndSaysTruncated()
    {
        var sut = new SandboxStepRunner(new FakeSandbox(body: new string('x', 2_000_000)));

        var text = await sut.RunAsync("cat huge.log", null, CancellationToken.None);

        text.Should().Contain("truncated: true");
        text.Should().Contain("(output truncated at 1 MB)");
        text.Length.Should().BeLessThan(1_100_000, "the 1 MB ceiling survived the source change");
    }

    [Fact]
    public async Task RunAsync_StderrLines_StillComeFromTheStream()
    {
        // The agent captures stdout into the body only, so stderr has no copy to switch to.
        var sut = new SandboxStepRunner(new FakeSandbox(
            body: "built\n", streamed: [Line(StepEventKind.Stderr, "warning CS9113")]));

        var text = await sut.RunAsync("dotnet build", null, CancellationToken.None);

        text.Should().Contain("stdout:\nbuilt");
        text.Should().Contain("stderr:\nwarning CS9113");
    }

    private static StepEvent Line(StepEventKind kind, string line) =>
        new(StepEvent.CurrentSchemaVersion, Guid.Empty, kind, line, DateTimeOffset.UtcNow);

    /// <summary>A project inventory the size of the one the live run lost (~27,000 chars).</summary>
    private static string SampleInventory()
    {
        var sb = new StringBuilder("--- projects ---\n");
        for (var i = 0; i < 200; i++)
            sb.Append("./Sample.AccessPortal.Server.Module").Append(i)
              .Append("/Sample.AccessPortal.Server.Module").Append(i).Append(".csproj\n");
        while (sb.Length < 27_100) sb.Append("./Sample.AccessPortal.Server.Extra.csproj\n");
        return sb.ToString();
    }

    private sealed class FakeSandbox(string? body, IReadOnlyList<StepEvent>? streamed = null) : ISandbox
    {
        public string JobId => "fake-job";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            foreach (var e in streamed ?? []) progress?.Report(e with { StepId = step.StepId });
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0, TimedOut: false,
                DurationSeconds: 0.26, ErrorMessage: null, OutputContent: body));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
