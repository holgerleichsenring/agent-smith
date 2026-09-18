using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-042eh: the phase diff is the interval the phase actually covers — from the head
/// recorded when the phase was selected to the working tree — with the run's own record left
/// out and the prompt bound taken at a file boundary.
/// </summary>
public sealed class PhaseDiffTests
{
    private const string Key = "api";

    [Fact]
    public async Task PhaseDiff_IsFromThePhaseStartHeadAndExcludesRunRecords()
    {
        var sandbox = new DiffSandbox(
            Chunk("src/Api/Handler.cs") + Chunk(".agentsmith/runs/2026-09-17/result.md"));
        var pipeline = WithStartHead("phase-start-sha");

        var diffs = await Diffs().TakeAsync(pipeline, Map(sandbox), CancellationToken.None);

        sandbox.DiffArgs.Should().Equal("diff", "--no-color", "phase-start-sha");
        diffs.Should().ContainSingle();
        diffs[0].Paths.Should().Equal("src/Api/Handler.cs");
        diffs[0].From.Should().Contain("this phase started");
    }

    [Fact]
    public async Task PhaseDiff_SandboxWithoutAStartHead_UsesTheDeliveryDiffAndSaysSo()
    {
        var sandbox = new DiffSandbox(Chunk("src/Api/Handler.cs"));

        var diffs = await Diffs().TakeAsync(new PipelineContext(), Map(sandbox), CancellationToken.None);

        sandbox.DiffArgs.Should().NotContain("phase-start-sha");
        diffs[0].From.Should().Contain("no head was recorded",
            "a reviewer shown the whole branch must know some of it belongs to an earlier phase");
    }

    [Fact]
    public void PhaseDiff_TheProjectsOwnMetaFiles_AreReviewable()
    {
        var files = PhaseDiffText.Files(
            Chunk(".agentsmith/contexts/default/principles.md")
            + Chunk(".agentsmith/runs/2026-09-17/result.md")
            + Chunk("src/Api/Handler.cs"));

        files.Select(f => f.Path).Should().Equal(
            [".agentsmith/contexts/default/principles.md", "src/Api/Handler.cs"],
            "only the per-run record is bookkeeping — an init phase's DELIVERABLE lives in "
            + ".agentsmith/ too, and dropping it would show that phase as having changed nothing");
    }

    [Fact]
    public void PhaseDiff_ABinaryHunk_IsNotShown()
    {
        var files = PhaseDiffText.Files(
            "diff --git a/docs/logo.png b/docs/logo.png\nGIT binary patch\nliteral 120\n"
            + Chunk("src/Api/Handler.cs"));

        files.Select(f => f.Path).Should().Equal(["src/Api/Handler.cs"],
            "a binary hunk carries no line to cite, so no finding on it could ever be admitted");
    }

    [Fact]
    public void PhaseDiff_OneOversizedFile_DoesNotBlankTheOnesBehindIt()
    {
        var files = PhaseDiffText.Files(
            Chunk("docs/assets/generated.svg", body: new string('x', 4000))
            + Chunk("src/Api/Handler.cs"));

        var bound = PhaseDiffText.Bound(files, maxChars: 500);

        bound.Paths.Should().Equal(["src/Api/Handler.cs"],
            "the skip is per file — one generated artefact early in the diff must not leave "
            + "every ordinary source file behind it unreviewed");
        bound.Unreviewed.Should().Equal("docs/assets/generated.svg");
    }

    [Fact]
    public void PhaseDiff_AQuotedPath_IsStillNamed()
    {
        var files = PhaseDiffText.Files(
            "diff --git \"a/src/Api/two words.cs\" \"b/src/Api/two words.cs\"\n"
            + "--- \"a/src/Api/two words.cs\"\n+++ \"b/src/Api/two words.cs\"\n@@ -1 +1 @@\n+x\n");

        files.Select(f => f.Path).Should().Equal(["src/Api/two words.cs"],
            "a path git quotes would otherwise vanish from the prompt AND the path list, so a "
            + "true finding on it could never be admitted");
    }

    [Fact]
    public void PhaseDiff_ADeletedFile_IsNamedByTheSideThatHasAName()
    {
        var files = PhaseDiffText.Files(
            "diff --git a/src/Api/Gone.cs b/src/Api/Gone.cs\n"
            + "deleted file mode 100644\n--- a/src/Api/Gone.cs\n+++ /dev/null\n@@ -1 +0,0 @@\n-x\n");

        files.Select(f => f.Path).Should().Equal("src/Api/Gone.cs");
    }

    [Fact]
    public void PhaseDiffPath_OctalEscapes_DecodeAsUtf8() =>
        PhaseDiffPath.Unquote("\"b/src/caf\\303\\251.cs\"").Should().Be("b/src/caf\u00e9.cs");

    [Fact]
    public void PhaseDiff_OverTheBound_ListsUnreviewedFiles()
    {
        var files = PhaseDiffText.Files(
            Chunk("src/A.cs", body: new string('x', 400)) + Chunk("src/B.cs", body: new string('y', 400)));

        var bound = PhaseDiffText.Bound(files, maxChars: files[0].Text.Length);

        bound.Paths.Should().Equal("src/A.cs");
        bound.Unreviewed.Should().Equal("src/B.cs");
        bound.Text.Should().NotContain("src/B.cs", "a file past the bound is named, never half shown");
    }

    [Fact]
    public void PhaseDiffText_WholeFilesFit_NothingIsUnreviewed()
    {
        var bound = PhaseDiffText.Bound(
            PhaseDiffText.Files(Chunk("src/A.cs") + Chunk("src/B.cs")), PhaseDiffText.MaxChars);

        bound.Paths.Should().Equal("src/A.cs", "src/B.cs");
        bound.Unreviewed.Should().BeEmpty();
    }

    [Fact]
    public void PhaseDiffText_ARename_IsNamedByWhereItEndsUp()
    {
        var files = PhaseDiffText.Files("diff --git a/src/Old.cs b/src/New.cs\nsimilarity index 98%\n");

        files.Should().ContainSingle().Which.Path.Should().Be("src/New.cs",
            "the a-side names a path the reviewer cannot open");
    }

    [Fact]
    public async Task PhaseStartHeads_RecordEachSandboxHead_AndReadItBack()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, Map(new DiffSandbox(string.Empty)));
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, new Dictionary<string, RemoteContextDiscovery>());

        await TestHelpers.TestPhaseReview.StartHeads().RecordAsync(pipeline, CancellationToken.None);

        PhaseStartHeads.For(pipeline, Key).Should().Be("head-sha");
    }

    private static PhaseDiffs Diffs() => new(
        new DeliveryDiff(
            new SandboxBaseLadder(
                new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance),
                NullLogger<SandboxBaseLadder>.Instance),
            new SandboxRunStartCommit(NullLogger<SandboxRunStartCommit>.Instance),
            NullLogger<DeliveryDiff>.Instance),
        NullLogger<PhaseDiffs>.Instance);

    private static PipelineContext WithStartHead(string head)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.PhaseStartHeads,
            new Dictionary<string, string>(StringComparer.Ordinal) { [Key] = head });
        return pipeline;
    }

    private static IReadOnlyDictionary<string, ISandbox> Map(ISandbox sandbox) =>
        new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Key] = sandbox };

    private static string Chunk(string path, string? body = null) =>
        $"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n@@ -1 +1 @@\n+{body ?? "line"}\n";

    /// <summary>Answers rev-parse with a head and every diff with one canned text, recording
    /// the argv so the test can say WHICH interval was asked for.</summary>
    private sealed class DiffSandbox(string diff) : ISandbox
    {
        public IReadOnlyList<string> DiffArgs { get; private set; } = [];
        public string JobId => "diff-sandbox";

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            var args = step.Args ?? [];
            if (args.Contains("diff")) DiffArgs = args;
            var output = args.Contains("rev-parse") ? "head-sha\n" : diff;
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0, TimedOut: false,
                DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
