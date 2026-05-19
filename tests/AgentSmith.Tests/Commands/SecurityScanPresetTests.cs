using System.Reflection;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

public sealed class SecurityScanPresetTests
{
    [Fact]
    public void SecurityScan_LoadersAfterCheckoutSource()
    {
        // p0131b: BootstrapProject + LoadCodeMap retired. Loaders run after the
        // source-checkout step.
        var preset = PipelinePresets.SecurityScan.ToList();
        var checkoutIdx = preset.IndexOf(CommandNames.CheckoutSource);
        var loadCtxIdx = preset.IndexOf(CommandNames.LoadContext);
        var loadCpIdx = preset.IndexOf(CommandNames.LoadCodingPrinciples);

        checkoutIdx.Should().BeGreaterThanOrEqualTo(0);
        loadCtxIdx.Should().BeGreaterThan(checkoutIdx);
        loadCpIdx.Should().BeGreaterThan(checkoutIdx);
        preset.Should().NotContain(CommandNames.BootstrapProject);
        preset.Should().NotContain(CommandNames.LoadCodeMap);
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
    public void SecuritySkillPromptStrategy_ImplementsBuildDomainSectionParts()
    {
        // p0147d: BuildDomainSectionParts moved from SecuritySkillRoundHandler to
        // SecuritySkillPromptStrategy — the strategy is the responsibility holder
        // injected into the (now thin) handler base.
        var method = typeof(AgentSmith.Application.Services.SkillRounds.Strategies.SecuritySkillPromptStrategy)
            .GetMethod("BuildDomainSectionParts", BindingFlags.Instance | BindingFlags.Public);
        method.Should().NotBeNull();
        method!.DeclaringType.Should().Be(
            typeof(AgentSmith.Application.Services.SkillRounds.Strategies.SecuritySkillPromptStrategy));
    }
}
