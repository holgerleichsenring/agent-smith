using AgentSmith.Application.Services.WorkSpecs;
using AgentSmith.Contracts.WorkSpecs;
using FluentAssertions;

namespace AgentSmith.Tests.WorkSpecs;

// p0390: one rule, one home. The RULE lives in spec.yaml, its SAMPLE in spec.md
// under a sample: heading — so a dangling anchor means the master would work from
// half a contract, and that must be a loud rejection, not a silent truncation.
public sealed class WorkSpecValidatorTests
{
    private readonly WorkSpecValidator _validator = new();

    private static WorkSpec SpecWith(
        IReadOnlyList<string>? requirements = null,
        IReadOnlyList<WorkSpecConstraint>? constraints = null,
        IReadOnlyList<string>? assumptions = null,
        string goal = "do the thing") => new(
        "p-1", goal, requirements ?? ["it works"], constraints ?? [], [], false,
        assumptions ?? [], [new WorkSpecRevision(1, "initial", DateTimeOffset.UnixEpoch)]);

    [Fact]
    public void Validate_ConstraintWithResolvableAnchor_Passes() =>
        _validator.Validate(
            SpecWith(constraints: [new WorkSpecConstraint("names stay verbatim", "queue-names")]),
            "## sample:queue-names\n\n```\nfoo.bar\n```\n")
        .Should().BeEmpty();

    [Fact]
    public void Validate_ConstraintWithDanglingAnchor_IsRejected() =>
        _validator.Validate(
            SpecWith(constraints: [new WorkSpecConstraint("names stay verbatim", "queue-names")]),
            "## sample:something-else\n")
        .Should().ContainSingle().Which.Should().Contain("queue-names");

    [Fact]
    public void Validate_ConstraintWithoutAnchor_NeedsNoSample() =>
        _validator.Validate(SpecWith(constraints: [new WorkSpecConstraint("no nulls", null)]), null)
            .Should().BeEmpty();

    [Fact]
    public void Validate_EmptyGoal_IsRejected() =>
        _validator.Validate(SpecWith(goal: "  "), null)
            .Should().Contain(e => e.Contains("goal"));

    [Fact]
    public void Validate_NoRequirementsAndNoHandback_IsRejected() =>
        _validator.Validate(SpecWith(requirements: []), null)
            .Should().Contain(e => e.Contains("requirements"));

    [Fact]
    public void Validate_NoRequirementsButHandedBack_IsAccepted()
    {
        var handedBack = SpecWith(requirements: []) with
        {
            Handback = new WorkSpecHandback(WorkSpecHandbackCase.NotUnderstood, "unreadable"),
        };

        _validator.Validate(handedBack, null).Should().BeEmpty();
    }

    [Fact]
    public void Validate_TooManyRequirements_IsRejectedNotTruncated()
    {
        var many = Enumerable.Range(0, WorkSpec.MaxRequirements + 1).Select(i => $"r{i}").ToList();

        _validator.Validate(SpecWith(requirements: many), null)
            .Should().ContainSingle().Which.Should().Contain($"cap is {WorkSpec.MaxRequirements}");
    }

    [Fact]
    public void Validate_ProseLengthRequirement_IsRejected() =>
        _validator.Validate(SpecWith(requirements: [new string('x', WorkSpec.MaxStatementLength + 1)]), null)
            .Should().ContainSingle().Which.Should().Contain("prose");

    [Fact]
    public void Parse_AnchorHeadings_AreCaseInsensitiveAndTrimmed()
    {
        var anchors = WorkSpecSampleAnchors.Parse("### Sample:Queue-Names  \ntext\n## sample:other\n");

        anchors.Should().HaveCount(2);
        anchors.Should().Contain("queue-names", "anchor lookup ignores case");
        anchors.Should().Contain("other");
    }

    [Fact]
    public void Parse_NoSamplesMarkdown_YieldsNoAnchors() =>
        WorkSpecSampleAnchors.Parse(null).Should().BeEmpty();
}
