using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-10-12-967ah: a pinned skills release older than v5.13.0 still speaks the old names, which
/// no reader accepts any more — the configuration is refused rather than left to derive nothing.
/// </summary>
public sealed class SkillsConfigValidatorTests
{
    private readonly SkillsConfigValidator _sut = new();

    [Theory]
    [InlineData("v5.12.3")]
    [InlineData("5.9.0")]
    [InlineData("v4.0.0")]
    public void SkillsConfigValidator_PinBelow5_13_IsRefused(string version)
    {
        var finding = _sut.Findings(new SkillsConfig { Source = SkillsSourceMode.Default, Version = version }).Single();

        finding.Severity.Should().Be(StartupFindingSeverity.Blocking);
        finding.Field.Should().Be("skills.version");
        finding.Reason.Should().Contain(version).And.Contain(SkillsConfigValidator.MinimumVersion);
    }

    [Theory]
    [InlineData("v5.13.0")]
    [InlineData("v5.16.0")]
    [InlineData("v5.18.0")]
    [InlineData("v5.19.0")]
    [InlineData("v6.0.0")]
    [InlineData("main")]
    public void SkillsConfigValidator_PinAtOrAbove5_13_OrNotAVersion_Passes(string version) =>
        _sut.Findings(new SkillsConfig { Source = SkillsSourceMode.Default, Version = version }).Should().BeEmpty();

    [Fact]
    public void SkillsConfigValidator_AnOldVersionThatIsNotPulled_IsNotJudged() =>
        _sut.Findings(new SkillsConfig { Source = SkillsSourceMode.Embedded, Version = "v5.0.0" }).Should().BeEmpty();

    [Fact]
    public void AgentSmithConfigValidator_AnOldSkillsPin_IsAFinding() =>
        new AgentSmithConfigValidator(_sut)
            .Validate(new AgentSmithConfig { Skills = new SkillsConfig { Source = SkillsSourceMode.Default, Version = "v5.12.0" } })
            .Should().ContainSingle(reason => reason.Contains(SkillsConfigValidator.MinimumVersion));
}
