using AgentSmith.Application.Services.Handlers;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0129c: VerifierPromptBuilder gains optional codingPrinciples parameter.
/// architecture-verifier reads the section; scope/build/test-verifier ignore it.
/// </summary>
public sealed class VerifierPromptBuilderTests
{
    [Fact]
    public void Build_NoCodingPrinciples_PromptHasNoCodingSection()
    {
        var (_, user) = VerifierPromptBuilder.Build("body", "{}", "{}");

        user.Should().NotContain("Coding principles");
    }

    [Fact]
    public void Build_CodingPrinciplesPresent_PromptIncludesCodingSectionBetweenPlanAndDiff()
    {
        var (_, user) = VerifierPromptBuilder.Build(
            "body", "{\"plan\":1}", "{\"diff\":2}", "Class size <= 120 lines");

        user.Should().Contain("Coding principles");
        user.Should().Contain("Class size <= 120 lines");
        var planIdx = user.IndexOf("## Plan");
        var principlesIdx = user.IndexOf("## Coding principles");
        var diffIdx = user.IndexOf("## Diff");
        principlesIdx.Should().BeGreaterThan(planIdx);
        principlesIdx.Should().BeLessThan(diffIdx);
    }

    [Fact]
    public void Build_CodingPrinciplesEmpty_NoSection()
    {
        var (_, user) = VerifierPromptBuilder.Build("body", "{}", "{}", "");

        user.Should().NotContain("Coding principles");
    }

    [Fact]
    public void Build_PlanAndDiffEmpty_StillRendersPlaceholders()
    {
        var (_, user) = VerifierPromptBuilder.Build("body", "", "");

        user.Should().Contain("(no plan)");
        user.Should().Contain("(no diff)");
    }
}
