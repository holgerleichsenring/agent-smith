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
/// 2026-09-17-042eh: the spec diff is the interval the spec actually covers — from the head
/// recorded when the spec was selected to the working tree — with the run's own record left
/// out and the prompt bound taken at a file boundary.
/// </summary>
public sealed class SpecDiffTests
{
    private const string Key = "api";

    [Fact]
    public async Task SpecDiff_IsFromTheSpecStartHeadAndExcludesRunRecords()
    {
        var sandbox = new DiffSandbox(
            Chunk("src/Api/Handler.cs") + Chunk(".agentsmith/runs/2026-09-17/result.md"));
        var pipeline = WithStartHead("phase-start-sha");

        var diffs = await Diffs().TakeAsync(pipeline, Map(sandbox), CancellationToken.None);

        sandbox.DiffArgs.Should().Equal("diff", "--no-color", "phase-start-sha");
        diffs.Should().ContainSingle();
        diffs[0].Paths.Should().Equal("src/Api/Handler.cs");
        diffs[0].From.Should().Contain("this spec started");
    }

    [Fact]
    public async Task SpecDiff_SandboxWithoutAStartHead_UsesTheDeliveryDiffAndSaysSo()
    {
        var sandbox = new DiffSandbox(Chunk("src/Api/Handler.cs"));

        var diffs = await Diffs().TakeAsync(new PipelineContext(), Map(sandbox), CancellationToken.None);

        sandbox.DiffArgs.Should().NotContain("phase-start-sha");
        diffs[0].From.Should().Contain("no head was recorded",
            "a reviewer shown the whole branch must know some of it belongs to an earlier phase");
    }

    [Fact]
    public void SpecDiff_TheProjectsOwnMetaFiles_AreReviewable()
    {
        var files = SpecDiffText.Files(
            Chunk(".agentsmith/contexts/default/principles.md")
            + Chunk(".agentsmith/runs/2026-09-17/result.md")
            + Chunk("src/Api/Handler.cs"));

        files.Select(f => f.Path).Should().Equal(
            [".agentsmith/contexts/default/principles.md", "src/Api/Handler.cs"],
            "only the per-run record is bookkeeping — an init phase's DELIVERABLE lives in "
            + ".agentsmith/ too, and dropping it would show that phase as having changed nothing");
    }

    [Fact]
    public void SpecDiff_ABinaryHunk_IsNotShown()
    {
        var files = SpecDiffText.Files(
            "diff --git a/docs/logo.png b/docs/logo.png\nGIT binary patch\nliteral 120\n"
            + Chunk("src/Api/Handler.cs"));

        files.Select(f => f.Path).Should().Equal(["src/Api/Handler.cs"],
            "a binary hunk carries no line to cite, so no finding on it could ever be admitted");
    }

    [Fact]
    public void SpecDiff_OneOversizedFile_DoesNotBlankTheOnesBehindIt()
    {
        var files = SpecDiffText.Files(
            Chunk("docs/assets/generated.svg", body: new string('x', 4000))
            + Chunk("src/Api/Handler.cs"));

        var bound = SpecDiffText.Bound(files, maxChars: 500);

        bound.Paths.Should().Equal(["src/Api/Handler.cs"],
            "the skip is per file — one generated artefact early in the diff must not leave "
            + "every ordinary source file behind it unreviewed");
        bound.Unreviewed.Should().Equal("docs/assets/generated.svg");
    }

    [Fact]
    public void SpecDiff_AQuotedPath_IsStillNamed()
    {
        var files = SpecDiffText.Files(
            "diff --git \"a/src/Api/two words.cs\" \"b/src/Api/two words.cs\"\n"
            + "--- \"a/src/Api/two words.cs\"\n+++ \"b/src/Api/two words.cs\"\n@@ -1 +1 @@\n+x\n");

        files.Select(f => f.Path).Should().Equal(["src/Api/two words.cs"],
            "a path git quotes would otherwise vanish from the prompt AND the path list, so a "
            + "true finding on it could never be admitted");
    }

    [Fact]
    public void SpecDiff_ADeletedFile_IsNamedByTheSideThatHasAName()
    {
        var files = SpecDiffText.Files(
            "diff --git a/src/Api/Gone.cs b/src/Api/Gone.cs\n"
            + "deleted file mode 100644\n--- a/src/Api/Gone.cs\n+++ /dev/null\n@@ -1 +0,0 @@\n-x\n");

        files.Select(f => f.Path).Should().Equal("src/Api/Gone.cs");
    }

    [Fact]
    public void SpecDiffPath_OctalEscapes_DecodeAsUtf8() =>
        SpecDiffPath.Unquote("\"b/src/caf\\303\\251.cs\"").Should().Be("b/src/caf\u00e9.cs");

    [Fact]
    public void SpecDiff_OverTheBound_ListsUnreviewedFiles()
    {
        var files = SpecDiffText.Files(
            Chunk("src/A.cs", body: new string('x', 400)) + Chunk("src/B.cs", body: new string('y', 400)));

        var bound = SpecDiffText.Bound(files, maxChars: files[0].Text.Length);

        bound.Paths.Should().Equal("src/A.cs");
        bound.Unreviewed.Should().Equal("src/B.cs");
        bound.Text.Should().NotContain("src/B.cs", "a file past the bound is named, never half shown");
    }

    [Fact]
    public void SpecDiffText_WholeFilesFit_NothingIsUnreviewed()
    {
        var bound = SpecDiffText.Bound(
            SpecDiffText.Files(Chunk("src/A.cs") + Chunk("src/B.cs")), SpecDiffText.MaxChars);

        bound.Paths.Should().Equal("src/A.cs", "src/B.cs");
        bound.Unreviewed.Should().BeEmpty();
    }

    [Fact]
    public void SpecDiffText_ARename_IsNamedByWhereItEndsUp()
    {
        var files = SpecDiffText.Files("diff --git a/src/Old.cs b/src/New.cs\nsimilarity index 98%\n");

        files.Should().ContainSingle().Which.Path.Should().Be("src/New.cs",
            "the a-side names a path the reviewer cannot open");
    }

    [Fact]
    public async Task SpecStartHeads_RecordEachSandboxHead_AndReadItBack()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, Map(new DiffSandbox(string.Empty)));
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, new Dictionary<string, RemoteContextDiscovery>());

        await TestHelpers.TestSpecReview.StartHeads().RecordAsync(pipeline, CancellationToken.None);

        SpecStartHeads.For(pipeline, Key).Should().Be("head-sha");
    }

    private static SpecDiffs Diffs() => new(
        new DeliveryDiff(
            new SandboxBaseLadder(
                new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance),
                NullLogger<SandboxBaseLadder>.Instance),
            new SandboxRunStartCommit(NullLogger<SandboxRunStartCommit>.Instance),
            NullLogger<DeliveryDiff>.Instance),
        NullLogger<SpecDiffs>.Instance);

    private static PipelineContext WithStartHead(string head)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SpecStartHeads,
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
