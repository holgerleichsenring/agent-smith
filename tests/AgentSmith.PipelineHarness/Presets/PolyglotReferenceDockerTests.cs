using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-01-9723: the Angular and Maven reference repositories, run through the code
/// preset on the gates THEY declare.
/// <para>
/// Neither stack needed a catalog entry, a domain or a language table row. Each names its
/// image and its ordered stages in its own <c>.agentsmith/contexts/&lt;name&gt;/context.yaml</c>
/// — which is what removed the ceiling: the inferred pair is build and test, and the
/// Angular reference's lint stage had no slot in it.
/// </para>
/// <para>
/// DOCKER TIER, and that is not a choice. The claim is that a declared command RAN and
/// that a defect made it exit non-zero; the fast tier's StubSandbox answers every step
/// with exit 0, so it can watch the stages be resolved but can never watch one go red.
/// This tier is opt-in (<c>AGENTSMITH_HARNESS_DOCKER=1</c>) and is NOT a CI gate — CI sets
/// neither the flag nor a docker daemon, and these tests log the gap rather than pass.
/// </para>
/// <para>
/// No LLM: the derivation and the master are scripted, the analyzer is stubbed by the
/// docker registrations, and the delivery account is the harness's own. What is real is
/// the container, the toolchain image, the checkout, and every declared command.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class PolyglotReferenceDockerTests(ITestOutputHelper output)
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"reference edit","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    /// <summary>A clean source addition per reference — enough for the branch to carry
    /// something a build can be green about, and green in every declared stage.</summary>
    private static readonly Dictionary<string, (string Path, string Content)> CleanEdit = new()
    {
        [FixturePaths.AngularReference] = ("src/app/currency.ts",
            "export function toCents(amount: number): number {\n  return Math.round(amount * 100);\n}\n"),
        [FixturePaths.JavaReference] = ("src/main/java/reference/Currency.java",
            "package reference;\n\npublic final class Currency {\n\n"
            + "    private Currency() {\n    }\n\n"
            + "    public static long toCents(long amount) {\n        return amount * 100L;\n    }\n}\n"),
    };

    [Fact]
    public async Task AngularReference_RunsItsDeclaredStages()
    {
        if (SkipIfUnavailable()) return;
        var run = await RunAsync(FixturePaths.AngularReference, CleanEditOf(FixturePaths.AngularReference));

        run.Result.IsSuccess.Should().BeTrue(
            $"a clean edit over the Angular reference must pass its own gates: {run.Result.Message}");
        run.StageLines.Should().Equal(
            ["build 'npm run build' exited 0", "lint 'npm run lint' exited 0", "test 'npm test' exited 0"],
            "the run executes the labels the repository declared, in the order it declared "
            + "them — lint included, which the inferred build/test pair has no slot for");
    }

    [Fact]
    public async Task JavaReference_RunsItsDeclaredStages()
    {
        if (SkipIfUnavailable()) return;
        var run = await RunAsync(FixturePaths.JavaReference, CleanEditOf(FixturePaths.JavaReference));

        run.Result.IsSuccess.Should().BeTrue(
            $"a clean edit over the Maven reference must pass its own gates: {run.Result.Message}");
        run.StageLines.Should().Equal(
            ["build 'mvn -B -DskipTests package' exited 0", "test 'mvn -B test' exited 0"],
            "the Maven reference is verified by its own two declared stages");
    }

    /// <summary>
    /// A green run is not the criterion — the named stages are. This is the other half:
    /// each defect is a source edit that reds ONE declared stage, and the phase's failing
    /// command has to name that stage rather than a neighbour or a cascade.
    /// </summary>
    [Theory]
    [InlineData(FixturePaths.AngularReference, "build", "npm run build")]
    [InlineData(FixturePaths.AngularReference, "lint", "npm run lint")]
    [InlineData(FixturePaths.AngularReference, "test", "npm test")]
    [InlineData(FixturePaths.JavaReference, "build", "mvn -B -DskipTests package")]
    [InlineData(FixturePaths.JavaReference, "test", "mvn -B test")]
    public async Task PolyglotReference_ADefect_RedsItsOwnStage(
        string reference, string stage, string command)
    {
        if (SkipIfUnavailable()) return;
        var run = await RunAsync(reference, Overlay(reference, stage));

        run.Result.IsSuccess.Should().BeFalse(
            $"the {stage} defect must refuse the run: {run.Result.Message}");
        run.FailingCommand.Should().Contain($"{stage} '{command}' exited",
            "the phase's failing command names the stage the repository declared, not a "
            + "stage behind it and not a generic build failure");
    }

    // ---- running one reference ----

    private async Task<Run> RunAsync(
        string reference, IReadOnlyList<(string Path, string Content)> edit)
    {
        await using var session = await DockerHarnessSession.CreateAsync(
            FixturePaths.ReferenceSource(reference));
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.DockerPolyglot), SandboxBackend.Docker, session);
        Script(harness, edit);

        var runner = new PipelineRunner(harness.Services) { RepoOverride = DockerHarnessRepo.For(session) };
        var result = await runner.RunAsync("code");

        output.WriteLine($"pipeline result: {result.IsSuccess} — {result.Message}");
        var shown = harness.Services.GetRequiredService<HarnessSpecAccountant>().CommandResultsShown;
        return new Run(result, StageLines(shown), FailingCommand(runner.LastContext!));
    }

    /// <param name="StageLines">The verify outcomes the run's delivery account was shown,
    /// one per stage that ran.</param>
    /// <param name="FailingCommand">The phase's failing command, empty when none failed.</param>
    private sealed record Run(
        CommandResult Result, IReadOnlyList<string> StageLines, string FailingCommand);

    private static IReadOnlyList<(string, string)> CleanEditOf(string reference) =>
        [CleanEdit[reference]];

    /// <summary>
    /// The one FIFO shape a scripted code run needs: the derivation, the master's edit,
    /// and a verdict that ends the loop. Everything the framework asks on its own behalf
    /// is answered by the harness stand-ins, so nothing else draws from this script.
    /// </summary>
    private static void Script(
        RealCompositionHarness harness, IReadOnlyList<(string Path, string Content)> files)
    {
        harness.ChatClient.EnqueueText(SpecDerivationFixture.DerivationJson);
        foreach (var (path, content) in files)
            harness.ChatClient.EnqueueToolCall("write_file", JsonSerializer.Serialize(
                new { path = "primary/" + path, content }));
        harness.ChatClient.EnqueueText(GreenVerdict);
    }

    /// <summary>Every file of a defect variant, as repo-relative path + content.</summary>
    private static IReadOnlyList<(string Path, string Content)> Overlay(string reference, string stage)
    {
        var root = FixturePaths.DefectOverlay(reference, stage);
        return [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.Ordinal)
            .Select(file => (
                Path.GetRelativePath(root, file).Replace('\\', '/'),
                File.ReadAllText(file)))];
    }

    // PhaseEvidence renders a verify outcome as "{repo}: {stage} '{command}' exited {code}".
    // The repository key is the harness session's, so the assertion is on what follows it.
    private static IReadOnlyList<string> StageLines(IReadOnlyList<string> shown) =>
        [.. shown
            .Where(line => line.Contains(" exited ", StringComparison.Ordinal))
            .Select(line => line[(line.IndexOf(": ", StringComparison.Ordinal) + 2)..])];

    private string FailingCommand(PipelineContext context)
    {
        if (!context.TryGet<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress, out var progress)
            || progress is null)
            return string.Empty;
        var failing = progress.Phases
            .FirstOrDefault(phase => phase.State == PhaseRunState.Failed)?.FailingCommand
            ?? string.Empty;
        if (failing.Length > 0) output.WriteLine("failing command: " + failing);
        return failing;
    }

    private bool SkipIfUnavailable()
    {
        if (DockerAvailability.IsAvailable(out var detail)) return false;
        output.WriteLine(DockerAvailability.CoverageNotExercised + " (" + detail + ")");
        return true;
    }
}
