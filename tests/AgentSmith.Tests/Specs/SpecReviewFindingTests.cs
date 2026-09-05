using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// The review reads a derived contract against the repository and reports the criteria no
/// work can satisfy, each with the search that shows it.
/// <para>
/// The failure this exists for: a dependency-upgrade phase whose done-list required the
/// package manifest to carry the upgraded versions, over a repository whose findings all sat
/// in transitive dependencies. No fix touches the manifest there, so the criterion was false
/// before the run started and the acceptance gate re-drove the master against it until an
/// operator cancelled the run.
/// </para>
/// </summary>
public sealed class SpecReviewFindingTests
{
    private const string ShapeCriterion =
        "the package manifest and the lockfile carry the upgraded dependency versions";
    private const string OutcomeCriterion =
        "an audit of the repository reports zero high-severity findings";

    private static IReadOnlyList<CriterionReview> Read(string json) =>
        SpecReviewReader.Read(json) ?? throw new InvalidOperationException("unreadable");

    [Fact]
    public void SpecReview_ACriterionPrescribingWhichFilesChange_IsReportedWithTheObservationThatContradictsIt()
    {
        var rows = Read($$"""
            [{"criterion": "{{ShapeCriterion}}", "disposition": "prescribes_shape",
              "observation": "search_branch '\"lodash\"' -- package.json",
              "output": "no match in package.json",
              "replacement": "an audit of the repository reports zero high-severity findings"}]
            """);

        rows.Should().ContainSingle();
        rows[0].Disposition.Should().Be(SpecReviewDisposition.PrescribesShape);
        rows[0].IsFinding.Should().BeTrue();
        rows[0].Observation.Should().Contain("search_branch");
        rows[0].Output.Should().Contain("no match");
    }

    [Fact]
    public void SpecReview_AManifestCriterionOverTransitiveFindings_IsReturnedAsAFinding()
    {
        var review = new SpecReview("p1", Read($$"""
            [{"criterion": "{{ShapeCriterion}}", "disposition": "prescribes_shape",
              "observation": "search_branch for the flagged packages in package.json",
              "output": "none of the 6 flagged packages is a direct dependency",
              "replacement": "{{OutcomeCriterion}}"}]
            """));

        review.Findings.Should().ContainSingle();
        review.IsQuiet.Should().BeFalse();
        // Correctable, because the review named the observation that decides it instead.
        review.Correctable.Should().ContainSingle();
        review.ForTheAuthor.Should().BeEmpty();
    }

    [Fact]
    public void SpecReview_ACriterionNoObservationCanSettle_IsReported()
    {
        var rows = Read("""
            [{"criterion": "the low-severity findings worth fixing are fixed",
              "disposition": "no_observation_settles",
              "observation": "search_branch for a stated threshold",
              "output": "no threshold is declared anywhere in the repository"}]
            """);

        rows[0].Disposition.Should().Be(SpecReviewDisposition.NoObservationSettles);
        rows[0].IsFinding.Should().BeTrue();
    }

    [Fact]
    public void SpecReview_ACriterionAlreadyTrueBeforeAnyWork_IsReported()
    {
        var rows = Read($$"""
            [{"criterion": "{{OutcomeCriterion}}", "disposition": "already_true",
              "observation": "search_branch for advisories in the lockfile",
              "output": "the lockfile already carries no flagged version"}]
            """);

        rows[0].IsFinding.Should().BeTrue();
        // Reported, never corrected: dropping it would quietly shrink the contract.
        rows[0].IsCorrectable.Should().BeFalse();
    }

    [Fact]
    public void SpecReview_AnUnsatisfiableCriterion_IsNotReportedAsMerelyOutstanding()
    {
        var review = new SpecReview("p1", Read($$"""
            [{"criterion": "{{ShapeCriterion}}", "disposition": "prescribes_shape",
              "observation": "search_branch for the flagged packages in package.json",
              "output": "none is a direct dependency"}]
            """));

        var reason = SpecReviewHandbackReason.For("p1", review.ForTheAuthor);

        // The delivery account can only call this outstanding, which reads as work not yet
        // done and buys a repair pass that cannot close it. This says what it actually is.
        reason.Should().Contain("no work can satisfy");
        reason.Should().Contain("not what must be true afterwards");
        reason.Should().Contain("none is a direct dependency");
    }

    [Fact]
    public void SpecReview_AnOutcomeCriterionThatIsFalseAtBaseline_PassesUntouched()
    {
        var rows = Read($$"""
            [{"criterion": "{{OutcomeCriterion}}", "disposition": "decidable"}]
            """);

        rows[0].IsFinding.Should().BeFalse();
        new SpecReview("p1", rows).IsQuiet.Should().BeTrue();
    }

    [Fact]
    public void SpecReview_AnAnswerNobodyCanInterpret_ReadsAsDecidable()
    {
        // The floor is pass-through: a wrong finding costs a human's working day.
        var rows = Read("""[{"criterion": "x", "disposition": "deeply suspicious"}]""");

        rows[0].Disposition.Should().Be(SpecReviewDisposition.Decidable);
        rows[0].IsFinding.Should().BeFalse();
    }

    [Fact]
    public void SpecReview_ADispositionWithNothingBehindIt_IsNotAFinding()
    {
        var rows = Read("""[{"criterion": "x", "disposition": "prescribes_shape"}]""");

        rows[0].IsFinding.Should().BeFalse();
    }

    [Fact]
    public void SpecReview_ACriterionTheAnswerSkipped_ReadsAsDecidable()
    {
        var aligned = SpecReviewAlignment.Of(
            [OutcomeCriterion, ShapeCriterion],
            Read($$"""
                [{"criterion": "{{ShapeCriterion}}", "disposition": "prescribes_shape",
                  "observation": "search_branch", "output": "no match"}]
                """));

        aligned.Should().HaveCount(2);
        aligned[0].Criterion.Should().Be(OutcomeCriterion);
        aligned[0].Disposition.Should().Be(SpecReviewDisposition.Decidable);
        aligned[1].IsFinding.Should().BeTrue();
    }

    [Fact]
    public void SpecReview_ARowForACriterionNobodyAskedAbout_IsDropped()
    {
        var aligned = SpecReviewAlignment.Of(
            [OutcomeCriterion],
            Read("""
                [{"criterion": "a criterion the review invented", "disposition": "prescribes_shape",
                  "observation": "search_branch", "output": "no match"}]
                """));

        aligned.Should().ContainSingle().Which.Criterion.Should().Be(OutcomeCriterion);
        aligned[0].IsFinding.Should().BeFalse();
    }
}
