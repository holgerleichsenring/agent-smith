using System.Globalization;
using System.Text;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-10-07-6b9db: a TypeScript monorepo's <c>npm test</c> wrote ~1 MB of jest output to
/// stderr, and run_command handed all of it to the model. Each section now keeps its head and
/// tail within its own budget and names its true total; the build verdict at the end of stdout
/// survives (p0419) and no tail is invented where none was captured (p0491).
/// </summary>
public sealed class BoundedRunCommandOutputTests
{
    private const int CaptureCap = SizeLimits.RunStepCapturedStdoutMaxChars;

    [Fact]
    public async Task Render_StderrOf1MB_KeepsHeadAndTailWithin20000()
    {
        var stderr = Lines("error TS2304: Cannot find name 'first'", 1_100_000, "Tests: 3 failed, 120 passed");
        var text = await Run(new FakeSandbox(body: "ok\n", stderr: stderr));

        var section = text[(text.IndexOf("\nstderr:\n", StringComparison.Ordinal) + 9)..];
        section.Length.Should().BeLessThanOrEqualTo(SizeLimits.RunCommandStderrMaxChars);
        section.Should().StartWith("error TS2304").And.EndWith("Tests: 3 failed, 120 passed");
        section.Should().Contain($"of {Grouped(Total(stderr))} characters cut from the middle");
        text.Should().Contain($"stderr_chars: {Total(stderr)}\n").And.Contain("truncated: true\n");
    }

    [Fact]
    public async Task Render_StdoutOver60000_KeepsTailWithBuildVerdict()
    {
        var body = string.Concat(Lines("Restore complete", 200_000, "Build FAILED.")
            .Select(l => l + "\n"));
        var text = await Run(new FakeSandbox(body: body, exitCode: 1));

        var stdout = ToolchainCapabilityLine.ExtractStdout(text)!;
        stdout.Length.Should().BeLessThanOrEqualTo(SizeLimits.RunCommandStdoutMaxChars);
        stdout.Should().StartWith("Restore complete").And.EndWith("Build FAILED.");
        stdout.Should().Contain($"of {Grouped(body.Length)} characters cut from the middle");
    }

    [Fact]
    public void Render_SmallOutput_SameTextAndCountHeaders()
    {
        var result = Result(body: "built\n");
        var streamed = Streamed(stdout: ["built"], stderr: ["warning CS9113"]);

        var bounded = BoundedRunCommandOutput.Render(result, 12, streamed);

        bounded.Should().Contain("stdout_chars: 6\nstderr_chars: 15\n");
        bounded.Replace("stdout_chars: 6\nstderr_chars: 15\n", string.Empty)
            .Should().Be(RunCommandOutput.Render(result, 12, streamed));
    }

    [Fact]
    public async Task Render_StdoutPastBodyCap_TailAndTotalFromStream()
    {
        var stream = Lines("first line", 1_500_000, "Build FAILED.");
        var text = await Run(new FakeSandbox(body: AgentBody(stream), stdout: stream));

        var stdout = ToolchainCapabilityLine.ExtractStdout(text)!;
        stdout.Should().StartWith("first line").And.EndWith("Build FAILED.");
        stdout.Should().Contain($"of {Grouped(Total(stream))} characters cut from the middle");
        text.Should().Contain($"stdout_chars: {Total(stream)}\n");
    }

    [Fact]
    public async Task Render_BodyAtCapStreamShort_SaysTailNotCaptured()
    {
        var body = AgentBody(Lines("first line", 1_200_000, "BODY_END"));
        var text = await Run(new FakeSandbox(body: body, stdout: ["the stream lagged"]));

        var stdout = ToolchainCapabilityLine.ExtractStdout(text)!;
        text.Should().Contain($"stdout_chars: at least {body.Length} (the tail was not captured)\n");
        stdout.Should().StartWith("first line").And.Contain("characters cut from the end");
        stdout.Should().NotContain("the stream lagged", "a lagging stream is not the tail (p0491)");
        stdout.Length.Should().BeLessThanOrEqualTo(SizeLimits.RunCommandStdoutMaxChars);
    }

    [Fact]
    public async Task Render_ErrorLineOf200k_BoundedAt8000()
    {
        var text = await Run(new FakeSandbox(body: null, exitCode: 1, error: new string('e', 200_000)));

        var start = text.IndexOf("error: ", StringComparison.Ordinal) + 7;
        var end = text.IndexOf("\n\nstdout:\n", StringComparison.Ordinal);
        (end - start).Should().BeLessThanOrEqualTo(SizeLimits.RunCommandErrorLineMaxChars);
        text.Should().Contain("of 200,000 characters cut from the middle");
    }

    [Fact]
    public async Task Render_Bounded_ToolchainProbeAndPhaseLogStillParse()
    {
        var stderr = Lines("noise", 300_000, "last noise");
        var text = await Run(new FakeSandbox(body: "git git version 2.43.0\n", stderr: stderr, exitCode: 2));
        var log = new PhaseCommandLog();

        log.Record("api", "probe", text);

        text.Should().StartWith("exit_code: 2\n");
        ToolchainCapabilityLine.ExtractStdout(text).Should().Be("git git version 2.43.0");
        log.Evidence()[0].Should().Contain("exited 2");
        text.IndexOf("\nstderr:", StringComparison.Ordinal)
            .Should().Be(text.LastIndexOf("\nstderr:", StringComparison.Ordinal), "no marker carries the label");
    }

    [Fact]
    public async Task RunProgram_FindFilesOutput_IsNotSectionBounded()
    {
        var paths = string.Concat(Enumerable.Range(0, 5_000).Select(i => $"./src/Module{i}/File{i}.cs\n"));
        var sut = new SandboxStepRunner(new FakeSandbox(body: paths));

        var run = await sut.RunProgramAsync("find", ["."], null, CancellationToken.None);

        paths.Length.Should().BeGreaterThan(SizeLimits.RunCommandStdoutMaxChars);
        run.Rendered.Should().Contain(paths.TrimEnd('\n'));
        run.Rendered.Should().NotContain("characters cut").And.NotContain("stdout_chars");
        run.Rendered.Should().Contain("truncated: false\n");
    }

    private static Task<string> Run(FakeSandbox sandbox) =>
        new SandboxStepRunner(sandbox).RunAsync("npm test", null, CancellationToken.None);

    /// <summary>Lines of ~100 characters filling <paramref name="chars"/>, first and last given.</summary>
    internal static IReadOnlyList<string> Lines(string first, int chars, string last)
    {
        var lines = new List<string> { first };
        for (var i = 0; lines.Count * 101 < chars; i++) lines.Add($"line {i:D8} " + new string('x', 86));
        lines.Add(last);
        return lines;
    }

    private static long Total(IEnumerable<string> lines) => lines.Sum(l => (long)l.Length + 1);

    private static string Grouped(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    // The sandbox agent's own capture rule: append a whole line while under the cap.
    private static string AgentBody(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
            if (sb.Length < CaptureCap) sb.Append(line).Append('\n');
        return sb.ToString();
    }

    private static StepResult Result(string? body, int exitCode = 0, string? error = null) =>
        new(StepResult.CurrentSchemaVersion, Guid.Empty, exitCode, TimedOut: false,
            DurationSeconds: 0.1, ErrorMessage: error, OutputContent: body);

    private static StreamedStepOutput Streamed(IEnumerable<string> stdout, IEnumerable<string> stderr)
    {
        var streamed = new StreamedStepOutput();
        foreach (var line in stdout) streamed.Collector.Report(Line(StepEventKind.Stdout, line));
        foreach (var line in stderr) streamed.Collector.Report(Line(StepEventKind.Stderr, line));
        return streamed;
    }

    private static StepEvent Line(StepEventKind kind, string line) =>
        new(StepEvent.CurrentSchemaVersion, Guid.Empty, kind, line, DateTimeOffset.UtcNow);

    private sealed class FakeSandbox(
        string? body, IReadOnlyList<string>? stdout = null, IReadOnlyList<string>? stderr = null,
        int exitCode = 0, string? error = null) : ISandbox
    {
        public string JobId => "fake-job";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            foreach (var line in stdout ?? []) progress?.Report(Line(StepEventKind.Stdout, line));
            foreach (var line in stderr ?? []) progress?.Report(Line(StepEventKind.Stderr, line));
            return Task.FromResult(Result(body, exitCode, error) with { StepId = step.StepId });
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
