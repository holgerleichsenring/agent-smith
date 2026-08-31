using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-08-31-7097: the one sweep a sandbox already runs now also looks for the binaries
/// this repository's declared verify stages name, and REPORTS the ones the image does
/// not carry. A report, never a gate: the derivation cannot read every command shape and
/// the harness tiers run images that carry almost nothing on purpose.
/// </summary>
public sealed class SandboxToolchainProbeTests
{
    [Fact]
    public async Task Probe_AMissingBinary_WarnsNamingTheImageAndTheStage()
    {
        var warnings = new CapturingLogger<ToolchainFindingReporter>();
        // The image answers for dotnet and nothing else; the repository declares pnpm.
        var sandbox = new SweepingSandbox("dotnet 9.0.100");

        var section = await Probe(warnings).ProbeAsync(
            Pipeline("ghcr.io/an-org/dotnet:9.0", ("lint", "pnpm lint")),
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["api"] = sandbox },
            keyToRepo: null, CancellationToken.None);

        warnings.Warnings.Should().ContainSingle()
            .Which.Should().Contain("pnpm").And.Contain("lint")
            .And.Contain("ghcr.io/an-org/dotnet:9.0");
        section.Should().Contain("pnpm").And.Contain("NOT found");
    }

    [Fact]
    public async Task Probe_AMissingBinary_DoesNotFailTheRun()
    {
        var sandbox = new SweepingSandbox("dotnet 9.0.100");

        var section = await Probe(new CapturingLogger<ToolchainFindingReporter>()).ProbeAsync(
            Pipeline("ghcr.io/an-org/dotnet:9.0", ("lint", "pnpm lint")),
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["api"] = sandbox },
            keyToRepo: null, CancellationToken.None);

        // The capability line is still delivered — the finding rides alongside it.
        section.Should().Contain("This sandbox has: dotnet 9.0.100");
    }

    [Fact]
    public async Task Probe_AnInProcessBackend_ReportsNoImage()
    {
        var warnings = new CapturingLogger<ToolchainFindingReporter>();
        var sandbox = new SweepingSandbox("dotnet 9.0.100");
        // No image was recorded: that backend runs on the host and pulled nothing.
        var pipeline = Pipeline(image: null, ("lint", "pnpm lint"));

        await Probe(warnings).ProbeAsync(
            pipeline,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["api"] = sandbox },
            keyToRepo: null, CancellationToken.None);

        warnings.Warnings.Should().ContainSingle()
            .Which.Should().Contain("no toolchain image").And.Contain("on the host");
    }

    [Fact]
    public async Task Probe_ACommandItCannotRead_SaysSoRatherThanSkippingIt()
    {
        var sandbox = new SweepingSandbox("dotnet 9.0.100");

        var section = await Probe(new CapturingLogger<ToolchainFindingReporter>()).ProbeAsync(
            Pipeline("an-image:1", ("build", "cd src && npm ci")),
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["api"] = sandbox },
            keyToRepo: null, CancellationToken.None);

        section.Should().Contain("Not checked").And.Contain("cd src && npm ci");
    }

    [Fact]
    public async Task Probe_ADeclaredBinary_RidesTheSweepTheSandboxAlreadyRuns()
    {
        var sandbox = new SweepingSandbox("dotnet 9.0.100");

        await Probe(new CapturingLogger<ToolchainFindingReporter>()).ProbeAsync(
            Pipeline("an-image:1", ("lint", "pnpm lint")),
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["api"] = sandbox },
            keyToRepo: null, CancellationToken.None);

        sandbox.Commands.Should().ContainSingle("one probe, two readers — never a second sweep")
            .Which.Should().Contain("p dotnet").And.Contain("q pnpm");
    }

    [Fact]
    public void Probe_RunsAfterThePrerequisites_SoAGoodImageIsNeverAccused()
    {
        // The probe runs at MASTER start, and the masters are spliced in by PhaseSequence.
        // The prerequisite step installs the very tools the declared stages then use, so a
        // sweep ahead of it would report a binary the run installs a minute later.
        var code = PipelinePresets.Code;

        code.ToList().IndexOf(CommandNames.PhaseSequence).Should()
            .BeGreaterThan(code.ToList().IndexOf(CommandNames.EnsurePrerequisites));
    }

    private static SandboxToolchainProbe Probe(CapturingLogger<ToolchainFindingReporter> warnings) =>
        new(new ContextVerifyStagesResolver(),
            new ToolchainFindingReporter(warnings),
            NullLogger<SandboxToolchainProbe>.Instance);

    private static PipelineContext Pipeline(
        string? image, params (string Label, string Command)[] stages)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                ["api"] =
                [
                    new RemoteContextDiscovery("api", ".", "csharp",
                        Verify: [.. stages.Select(s => new ContextYamlVerifyStage(s.Label, s.Command))]),
                ],
            });
        if (image is not null)
            pipeline.Set<IReadOnlyDictionary<string, string>>(
                ContextKeys.SandboxImages,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["api"] = image });
        return pipeline;
    }

    // Answers the sweep the way a real image does: every tool it carries prints a line,
    // everything else stays silent.
    private sealed class SweepingSandbox(string reported) : ISandbox
    {
        public List<string> Commands { get; } = [];

        public string JobId => "probe";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(step);
            Commands.Add(step.Args is { Count: > 1 } args ? args[1] : string.Empty);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null,
                OutputContent: reported));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
