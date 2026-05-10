using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Pipeline;

/// <summary>
/// p0130a: BootstrapCheck + BootstrapGate insertion into code-touching presets.
/// Strict: FixBug, FixNoTest, AddFeature, SecurityScan, Autonomous (gate active).
/// Conditional: ApiSecurityScan (gate honors source_available; runtime decision in BootstrapGateHandler).
/// None: MadDiscussion, LegalAnalysis, InitProject, SkillManager (no gate).
/// </summary>
public sealed class BootstrapGatePipelinePresetsTests
{
    public static TheoryData<string, IReadOnlyList<string>> StrictPresets => new()
    {
        { "fix-bug", PipelinePresets.FixBug },
        { "fix-no-test", PipelinePresets.FixNoTest },
        { "add-feature", PipelinePresets.AddFeature },
        { "security-scan", PipelinePresets.SecurityScan },
        { "autonomous", PipelinePresets.Autonomous },
    };

    [Theory, MemberData(nameof(StrictPresets))]
    public void StrictPreset_ContainsBootstrapCheckImmediatelyFollowedByBootstrapGate(
        string _, IReadOnlyList<string> preset)
    {
        var checkIndex = preset.ToList().IndexOf(CommandNames.BootstrapCheck);
        checkIndex.Should().BeGreaterThan(0,
            "BootstrapCheck must appear in strict preset");
        preset[checkIndex + 1].Should().Be(CommandNames.BootstrapGate,
            "BootstrapGate must immediately follow BootstrapCheck so policy reads the just-published concepts");
    }

    [Theory, MemberData(nameof(StrictPresets))]
    public void StrictPreset_BootstrapGateRunsBeforeLoadStepsAndBootstrapProject(
        string _, IReadOnlyList<string> preset)
    {
        var list = preset.ToList();
        var gateIndex = list.IndexOf(CommandNames.BootstrapGate);
        gateIndex.Should().BeGreaterThan(0);

        // Each Load*-step (when present) must run AFTER the gate so missing files
        // get the structured "run init-project first" message instead of failing
        // somewhere downstream.
        foreach (var loadStep in new[] {
            CommandNames.LoadContext, CommandNames.LoadCodingPrinciples,
            CommandNames.LoadCodeMap, CommandNames.BootstrapProject
        })
        {
            var loadIndex = list.IndexOf(loadStep);
            if (loadIndex >= 0)
                loadIndex.Should().BeGreaterThan(gateIndex,
                    $"{loadStep} must run after BootstrapGate");
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
    [InlineData("skill-manager")]
    public void UngatedPreset_DoesNotContainBootstrapGate(string presetName)
    {
        var preset = PipelinePresets.TryResolve(presetName);
        preset.Should().NotBeNull();
        preset!.Should().NotContain(CommandNames.BootstrapGate);
        preset.Should().NotContain(CommandNames.BootstrapCheck);
    }
}
