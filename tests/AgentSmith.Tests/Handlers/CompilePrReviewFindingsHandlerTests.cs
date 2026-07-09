using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0167c: CompilePrReviewFindingsHandler applies the pr-review render budget
/// — group by file + line range, most severe first, cap 25 inline, fold the
/// rest (and everything without a diff-line anchor) into the summary. Uses
/// the real selector + renderer: the compilation IS the unit under test.
/// </summary>
public sealed class CompilePrReviewFindingsHandlerTests
{
    private const string RunId = "run-42";

    [Fact]
    public async Task CompilePrReviewFindings_GroupsByFileLineAndSortsBySeverity()
    {
        var observations = new List<SkillObservation>
        {
            Obs(1, ObservationSeverity.Medium, "src/A.cs", 10, 10, "Medium on A:10"),
            Obs(2, ObservationSeverity.Critical, "src/B.cs", 5, 6, "Critical on B:5..6"),
            Obs(3, ObservationSeverity.Low, "src/A.cs", 10, 10, "Low on A:10 (same anchor)"),
            Obs(4, ObservationSeverity.High, "src/A.cs", 20, 20, "High on A:20"),
        };
        var (handler, context) = CreateSut(observations, Diff(("src/A.cs", 30), ("src/B.cs", 30)));

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var summary = context.Pipeline.Get<PrReviewSummary>(ContextKeys.PrReviewSummary);
        summary.InlineComments.Should().HaveCount(3);

        summary.InlineComments[0].Should().BeEquivalentTo(
            new { File = "src/B.cs", StartLine = 5, EndLine = 6, Severity = "Critical" });
        summary.InlineComments[1].Should().BeEquivalentTo(
            new { File = "src/A.cs", StartLine = 20, EndLine = 20, Severity = "High" });
        summary.InlineComments[2].Should().BeEquivalentTo(
            new { File = "src/A.cs", StartLine = 10, EndLine = 10, Severity = "Medium" });

        // Same-anchor findings merge into ONE comment, most severe first.
        summary.InlineComments[2].Body.Should().Contain("Medium on A:10").And.Contain("Low on A:10");
        summary.TopLevelComment.Should().Contain("4 finding(s)")
            .And.Contain("Critical: 1").And.Contain(RunId);
    }

    [Fact]
    public async Task CompilePrReviewFindings_FortyObservations_TwentyFiveInlinePlusSummary()
    {
        var observations = Enumerable.Range(1, 40)
            .Select(i => Obs(i,
                i <= 25 ? ObservationSeverity.High : ObservationSeverity.Low,
                "src/Big.cs", i, i, $"Finding {i}"))
            .ToList();
        var (handler, context) = CreateSut(observations, Diff(("src/Big.cs", 60)));

        await handler.ExecuteAsync(context, CancellationToken.None);

        var summary = context.Pipeline.Get<PrReviewSummary>(ContextKeys.PrReviewSummary);
        summary.InlineComments.Should().HaveCount(25);
        summary.InlineComments.Should().OnlyContain(c => c.Severity == "High");
        summary.TopLevelComment.Should().Contain("40 finding(s)")
            .And.Contain("Remaining 15 finding(s)").And.Contain("result.md");
    }

    [Fact]
    public async Task CompilePrReviewFindings_AnchorOutsideDiffOrMissing_FoldsIntoSummary()
    {
        var observations = new List<SkillObservation>
        {
            Obs(1, ObservationSeverity.High, "src/A.cs", 10, 10, "Anchored finding"),
            Obs(2, ObservationSeverity.High, "src/A.cs", 500, 500, "Off-diff line"),
            Obs(3, ObservationSeverity.High, "src/NotInDiff.cs", 3, 3, "Unknown file"),
            Obs(4, ObservationSeverity.Medium, "", 0, 0, "No anchor at all"),
        };
        var (handler, context) = CreateSut(observations, Diff(("src/A.cs", 30)));

        await handler.ExecuteAsync(context, CancellationToken.None);

        var summary = context.Pipeline.Get<PrReviewSummary>(ContextKeys.PrReviewSummary);
        summary.InlineComments.Should().ContainSingle().Which.File.Should().Be("src/A.cs");
        summary.TopLevelComment.Should().Contain("Remaining 3 finding(s)")
            .And.Contain("Off-diff line").And.Contain("Unknown file").And.Contain("No anchor at all");
    }

    [Fact]
    public async Task CompilePrReviewFindings_ExecutionLimitMarkers_ExcludedFromReview()
    {
        var observations = new List<SkillObservation>
        {
            Obs(1, ObservationSeverity.High, "src/A.cs", 10, 10, "Real finding"),
            Obs(2, ObservationSeverity.High, "src/A.cs", 11, 11, "Budget marker",
                ExecutionLimitCategories.ExecutionLimitToolCalls),
        };
        var (handler, context) = CreateSut(observations, Diff(("src/A.cs", 30)));

        await handler.ExecuteAsync(context, CancellationToken.None);

        var summary = context.Pipeline.Get<PrReviewSummary>(ContextKeys.PrReviewSummary);
        summary.InlineComments.Should().ContainSingle();
        summary.TopLevelComment.Should().Contain("1 finding(s)").And.NotContain("Budget marker");
    }

    [Fact]
    public async Task CompilePrReviewFindings_NoObservations_CompilesCleanSummary()
    {
        var (handler, context) = CreateSut([], Diff(("src/A.cs", 30)));

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var summary = context.Pipeline.Get<PrReviewSummary>(ContextKeys.PrReviewSummary);
        summary.InlineComments.Should().BeEmpty();
        summary.TopLevelComment.Should().Contain("No findings").And.Contain(RunId);
    }

    private static (CompilePrReviewFindingsHandler Handler, CompilePrReviewFindingsContext Context)
        CreateSut(List<SkillObservation> observations, PrDiffAnalysis diff)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);
        if (observations.Count > 0)
            pipeline.Set(ContextKeys.SkillObservations, observations);
        var handler = new CompilePrReviewFindingsHandler(
            new PrReviewFindingSelector(), new PrReviewCommentRenderer(),
            NullLogger<CompilePrReviewFindingsHandler>.Instance);
        return (handler, new CompilePrReviewFindingsContext(diff, pipeline));
    }

    private static SkillObservation Obs(
        int id, ObservationSeverity severity, string file, int start, int end,
        string description, string? category = "correctness") => new(
        id, "reviewer", ObservationConcern.Correctness, description, "Fix it",
        Blocking: false, Severity: severity, Confidence: 85,
        File: string.IsNullOrEmpty(file) ? null : file,
        LineRange: start > 0 ? new ObservationLineRange(start, end) : null,
        Category: category);

    private static PrDiffAnalysis Diff(params (string Path, int MaxLine)[] files) => new(
        "base", "head",
        files.Select(f => new PrDiffFile(
            f.Path, PrFileChangeKind.Modified, IsBinary: false,
            [new PrHunk(1, 0, 1, f.MaxLine,
                Enumerable.Range(1, f.MaxLine)
                    .Select(i => new PrDiffLine(PrDiffLineKind.Added, null, i, $"line {i}"))
                    .ToList())])).ToList());
}
