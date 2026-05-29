using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

public sealed class ApiSecurityScanPresetTests
{
    [Fact]
    public void ApiSecurityScan_LoadersAfterTryCheckoutSource()
    {
        // p0131b: LoadCodeMap retired together with CodeMapGenerator.
        var preset = PipelinePresets.ApiSecurityScan.ToList();
        var checkoutIdx = preset.IndexOf(CommandNames.TryCheckoutSource);
        var loadCtxIdx = preset.IndexOf(CommandNames.LoadContext);
        var loadCpIdx = preset.IndexOf(CommandNames.LoadCodingPrinciples);

        checkoutIdx.Should().BeGreaterThanOrEqualTo(0);
        loadCtxIdx.Should().BeGreaterThan(checkoutIdx);
        loadCpIdx.Should().BeGreaterThan(checkoutIdx);
        preset.Should().NotContain(CommandNames.LoadCodeMap);
    }

    [Fact]
    public void ApiSecurityScan_AgenticMasterImmediatelyBeforeDeliverFindings_PostP0179d()
    {
        // p0179d: CompressApiScanFindings + LoadSkills + Triage / Review / Final /
        // Convergence / CompileFindings retired. The pattern is
        // scanners → AgenticMaster (api-security-master) → DeliverFindings.
        var preset = PipelinePresets.ApiSecurityScan.ToList();
        var masterIdx = preset.IndexOf(CommandNames.AgenticMaster);
        var deliverIdx = preset.IndexOf(CommandNames.DeliverFindings);

        masterIdx.Should().BeGreaterThanOrEqualTo(0);
        deliverIdx.Should().Be(masterIdx + 1);
        preset.Should().NotContain(CommandNames.CompressApiScanFindings);
        preset.Should().NotContain(CommandNames.LoadSkills);
    }
}
