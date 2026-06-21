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
        // Convergence / CompileFindings retired. p0267: CollectMasterFindings now
        // sits between the master and delivery — scanners → AgenticMaster
        // (api-security-master) → CollectMasterFindings → DeliverFindings.
        var preset = PipelinePresets.ApiSecurityScan.ToList();
        var masterIdx = preset.IndexOf(CommandNames.AgenticMaster);
        var deliverIdx = preset.IndexOf(CommandNames.DeliverFindings);

        masterIdx.Should().BeGreaterThanOrEqualTo(0);
        deliverIdx.Should().Be(masterIdx + 2);
        preset.Should().NotContain(CommandNames.CompressApiScanFindings);
        preset.Should().NotContain(CommandNames.LoadSkills);
    }

    [Fact]
    public void ApiSecurityScan_Preset_CollectMasterFindings_BetweenMasterAndDeliver()
    {
        // p0267: the master's triaged observations only reach DeliverFindings via the
        // discrete CollectMasterFindings step, which must run AFTER the master (its
        // input) and BEFORE delivery (its consumer).
        var preset = PipelinePresets.ApiSecurityScan.ToList();
        var masterIdx = preset.IndexOf(CommandNames.AgenticMaster);
        var collectIdx = preset.IndexOf(CommandNames.CollectMasterFindings);
        var deliverIdx = preset.IndexOf(CommandNames.DeliverFindings);

        collectIdx.Should().BeGreaterThan(masterIdx);
        collectIdx.Should().BeLessThan(deliverIdx);
    }
}
