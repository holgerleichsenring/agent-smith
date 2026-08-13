using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Scope;

/// <summary>
/// p0413: the SAME scope-classification call that sizes the ticket also states its
/// SHAPE — a deterministic transformation, judgement, or a mix — with one line of
/// reason. Size decides what the run may spend; shape decides how the work is CUT.
/// An absent or unrecognised shape reads as "none stated", and every consumer then
/// behaves exactly as it did before (fail-safe, never a gate).
/// </summary>
public sealed class WorkShapeClassificationTests
{
    [Fact]
    public void Scope_DeterministicWork_IsClassifiedWithAReason()
    {
        var reply = """
            {"repos": [{"name": "server", "affected": true, "confidence": 0.9}],
             "complexity": "medium", "shape": "deterministic",
             "shape_reason": "the same declared upgrade applied across a known set",
             "rationale": "both components declare the same set"}
            """;

        var result = RepoScopeParser.TryParse(reply);

        result.Should().NotBeNull();
        result!.Shape.Should().NotBeNull();
        result.Shape!.Shape.Should().Be(WorkShape.Deterministic);
        result.Shape.Reason.Should().Be("the same declared upgrade applied across a known set");
    }

    [Theory]
    [InlineData("deterministic", WorkShape.Deterministic)]
    [InlineData("judgement", WorkShape.Judgement)]
    [InlineData("judgment", WorkShape.Judgement)] // the other spelling is the same verdict
    [InlineData("mixed", WorkShape.Mixed)]
    public void Shape_EachStatedShape_IsParsed(string raw, WorkShape expected)
    {
        var reply = $$"""{"repos": [{"name": "a", "affected": true}], "shape": "{{raw}}"}""";

        RepoScopeParser.TryParse(reply)!.Shape!.Shape.Should().Be(expected);
    }

    [Fact]
    public void Shape_Absent_LeavesTheCutUntouched()
    {
        var reply = """{"repos": [{"name": "a", "affected": true, "confidence": 0.9}]}""";

        RepoScopeParser.TryParse(reply)!.Shape.Should().BeNull(
            "no shape stated must read as 'no shape', not as a default one");
    }

    [Fact]
    public void Shape_Unrecognised_LeavesTheCutUntouched()
    {
        var reply = """{"repos": [{"name": "a", "affected": true}], "shape": "gnarly"}""";

        RepoScopeParser.TryParse(reply)!.Shape.Should().BeNull();
    }

    [Fact]
    public void Shape_WithoutReason_IsStillAVerdict()
    {
        var reply = """{"repos": [{"name": "a", "affected": true}], "shape": "mixed"}""";

        var shape = RepoScopeParser.TryParse(reply)!.Shape;

        shape!.Shape.Should().Be(WorkShape.Mixed);
        shape.Reason.Should().BeNull();
        shape.ToString().Should().Be("mixed");
    }

    [Fact]
    public void Verdict_RendersItsShapeAndReasonAsOneLine()
    {
        var verdict = new WorkShapeVerdict(WorkShape.Deterministic, "one operation over a known set");

        verdict.Name.Should().Be("deterministic");
        verdict.ToString().Should().Be("deterministic — one operation over a known set");
    }

    [Fact]
    public void ClassifierPrompt_AsksForShapeSeparatelyFromSize()
    {
        var prompt = RepoScopeSystemPrompt.Text;

        prompt.Should().Contain("\"shape\": \"deterministic|judgement|mixed\"");
        prompt.Should().Contain("shape_reason");
        prompt.Should().Contain("complexity is HOW MUCH, shape is",
            "the two estimates must not collapse into one another");
    }
}
