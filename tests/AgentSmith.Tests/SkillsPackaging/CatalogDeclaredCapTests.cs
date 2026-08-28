using AgentSmith.SkillsPackaging;
using FluentAssertions;

namespace AgentSmith.Tests.SkillsPackaging;

/// <summary>
/// p0518: the catalog repository ships the cap its own release gate enforces, and
/// this build refuses a catalog whose number is not the number the loader enforces.
/// Neither side can move alone: the disagreement stops the build that vendors it.
/// </summary>
public sealed class CatalogDeclaredCapTests
{
    [Fact]
    public void Violation_CatalogDeclaresTheSameCap_IsAccepted() =>
        CatalogDeclaredCap.Violation($"# a comment\n{MasterDescriptionValidator.MaxDescriptionChars}\n")
            .Should().BeNull();

    [Fact]
    public void Violation_CatalogDeclaresADifferentCap_FailsTheBuild()
    {
        var reason = CatalogDeclaredCap.Violation("240\n");

        reason.Should().Contain("240").And.Contain(MasterDescriptionValidator.MaxDescriptionChars.ToString(),
            "the build error must name both numbers so the operator knows which side to move");
    }

    [Fact]
    public void Violation_CatalogDeclaresNothingParsable_FailsTheBuild() =>
        CatalogDeclaredCap.Violation("# only comments\n").Should().Contain("declares no cap");

    [Fact]
    public void Violation_CatalogWithoutTheDeclaration_IsRefused() =>
        // 2026-08-28-a08d: it used to be accepted, because releases before p0518 shipped no
        // such file. From the embedded pin the file always exists, so its absence is an
        // incomplete package rather than an older release.
        CatalogDeclaredCap.Violation(null).Should().Contain("is missing");

    [Theory]
    [InlineData("skills/description-cap.txt")]
    [InlineData("./skills/description-cap.txt")]
    public void Matches_TheDeclarationEntry_IsFoundInTheTarball(string entryName) =>
        CatalogDeclaredCap.Matches(entryName).Should().BeTrue();

    [Fact]
    public void Matches_AnUnrelatedEntry_IsIgnored() =>
        CatalogDeclaredCap.Matches("skills/_masters/m/SKILL.md").Should().BeFalse();
}
