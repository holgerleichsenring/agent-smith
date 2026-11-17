using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Pipeline;

/// <summary>
/// p0130a: BootstrapCheck + BootstrapGate insertion into code-touching presets.
/// 2026-11-16-f3fba: code, spec-derive, pr-review and security-scan read the bootstrap on the host in
/// RunConfigPreflight, before CheckoutSource, and carry neither step any more. Conditional:
/// ApiSecurityScan keeps both (its source is optional, decided at checkout).
/// None: MadDiscussion, LegalAnalysis, InitProject (no gate).
/// </summary>
public sealed class BootstrapGatePipelinePresetsTests
{
    public static TheoryData<string, IReadOnlyList<string>> PreflightPresets => new()
    {
        { "code", PipelinePresets.Code },
        { "spec-derive", PipelinePresets.SpecDerive },
        { "pr-review", PipelinePresets.PrReview },
        { "security-scan", PipelinePresets.SecurityScan },
    };

    [Theory, MemberData(nameof(PreflightPresets))]
    public void Preset_RunConfigPreflightBeforeCheckoutSource_NoBootstrapGate(
        string _, IReadOnlyList<string> preset)
    {
        var list = preset.ToList();
        var configIndex = list.IndexOf(CommandNames.RunConfigPreflight);

        configIndex.Should().BeGreaterThan(0, "the config preflight runs in every preset that needs a bootstrap");
        configIndex.Should().BeLessThan(list.IndexOf(CommandNames.CheckoutSource),
            "it runs before the first sandbox");
        list.Should().NotContain(CommandNames.BootstrapGate).And.NotContain(CommandNames.BootstrapCheck);
    }

    [Theory, MemberData(nameof(PreflightPresets))]
    public void Preset_RunConfigPreflightBeforeLoadSteps(string _, IReadOnlyList<string> preset)
    {
        var list = preset.ToList();
        var configIndex = list.IndexOf(CommandNames.RunConfigPreflight);

        foreach (var loadStep in new[] {
            CommandNames.LoadContext, CommandNames.LoadCodingPrinciples,
            CommandNames.LoadCodeMap, CommandNames.BootstrapProject
        })
        {
            var loadIndex = list.IndexOf(loadStep);
            if (loadIndex >= 0)
                loadIndex.Should().BeGreaterThan(configIndex, $"{loadStep} must run after the bootstrap is proven");
        }
    }

    [Fact]
    public void CodeAndSpecDerive_RunConfigPreflightAfterScopeRepos_RunPreflightAfterCheckout()
    {
        foreach (var preset in new[] { PipelinePresets.Code, PipelinePresets.SpecDerive })
        {
            var list = preset.ToList();
            list.IndexOf(CommandNames.RunConfigPreflight).Should().Be(list.IndexOf(CommandNames.ScopeRepos) + 1);
            list.IndexOf(CommandNames.RunPreflight).Should().Be(list.IndexOf(CommandNames.CheckoutSource) + 1);
        }
    }

    [Fact]
    public void ApiSecurityScan_BootstrapCheckRunsAfterTryCheckoutSource()
    {
        // Required so source_available is published before the gate evaluates
        // its conditional precondition.
        var list = PipelinePresets.ApiSecurityScan.ToList();
        var tryCheckoutIndex = list.IndexOf(CommandNames.TryCheckoutSource);
        var checkIndex = list.IndexOf(CommandNames.BootstrapCheck);
        var gateIndex = list.IndexOf(CommandNames.BootstrapGate);

        tryCheckoutIndex.Should().BeGreaterThan(0);
        checkIndex.Should().BeGreaterThan(tryCheckoutIndex);
        gateIndex.Should().Be(checkIndex + 1);
    }

    [Theory]
    [InlineData("mad-discussion")]
    [InlineData("legal-analysis")]
    [InlineData("init-project")]
    public void UngatedPreset_DoesNotContainBootstrapGate(string presetName)
    {
        var preset = PipelinePresets.TryResolve(presetName);
        preset.Should().NotBeNull();
        preset!.Should().NotContain(CommandNames.BootstrapGate);
        preset.Should().NotContain(CommandNames.BootstrapCheck);
    }
}
