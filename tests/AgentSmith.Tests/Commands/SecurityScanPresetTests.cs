using System.Reflection;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

public sealed class SecurityScanPresetTests
{
    [Fact]
    public void SecurityScan_LoadersAfterBootstrapProject()
    {
        var preset = PipelinePresets.SecurityScan.ToList();
        var bootstrapIdx = preset.IndexOf(CommandNames.BootstrapProject);
        var loadCtxIdx = preset.IndexOf(CommandNames.LoadContext);
        var loadCpIdx = preset.IndexOf(CommandNames.LoadCodingPrinciples);
        var loadMapIdx = preset.IndexOf(CommandNames.LoadCodeMap);

        bootstrapIdx.Should().BeGreaterThanOrEqualTo(0);
        loadCtxIdx.Should().BeGreaterThan(bootstrapIdx);
        loadCpIdx.Should().BeGreaterThan(bootstrapIdx);
        loadMapIdx.Should().BeGreaterThan(bootstrapIdx);
    }

    [Fact]
    public void AllPresets_DoNotReferenceLoadDomainRulesString()
    {
        // The constant was deleted; ensure no preset still wires the literal string.
        var allPresets = new[]
        {
            PipelinePresets.FixBug, PipelinePresets.FixNoTest, PipelinePresets.AddFeature,
            PipelinePresets.SecurityScan, PipelinePresets.LegalAnalysis,
        };
        foreach (var preset in allPresets)
            preset.Should().NotContain("LoadDomainRulesCommand");
    }

    [Fact]
    public void CommandNames_DoesNotExposeLoadDomainRulesConstant()
    {
        var member = typeof(CommandNames).GetField(
            "LoadDomainRules",
            BindingFlags.Public | BindingFlags.Static);
        member.Should().BeNull();
    }

    [Fact]
    public void SecuritySkillRoundHandler_OverridesBuildDomainSectionParts()
    {
        var method = typeof(SecuritySkillRoundHandler).GetMethod(
            "BuildDomainSectionParts",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        method!.DeclaringType.Should().Be(typeof(SecuritySkillRoundHandler));
    }
}
