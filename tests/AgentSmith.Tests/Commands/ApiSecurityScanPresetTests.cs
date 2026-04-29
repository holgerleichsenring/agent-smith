using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

public sealed class ApiSecurityScanPresetTests
{
    [Fact]
    public void ApiSecurityScan_LoadersAfterTryCheckoutSource()
    {
        var preset = PipelinePresets.ApiSecurityScan.ToList();
        var checkoutIdx = preset.IndexOf(CommandNames.TryCheckoutSource);
        var loadCtxIdx = preset.IndexOf(CommandNames.LoadContext);
        var loadCpIdx = preset.IndexOf(CommandNames.LoadCodingPrinciples);
        var loadMapIdx = preset.IndexOf(CommandNames.LoadCodeMap);

        checkoutIdx.Should().BeGreaterThanOrEqualTo(0);
        loadCtxIdx.Should().BeGreaterThan(checkoutIdx);
        loadCpIdx.Should().BeGreaterThan(checkoutIdx);
        loadMapIdx.Should().BeGreaterThan(checkoutIdx);
    }

    [Fact]
    public void ApiSecurityScan_CorrelateFindingsBetweenCompressAndLoadSkills()
    {
        var preset = PipelinePresets.ApiSecurityScan.ToList();
        var compressIdx = preset.IndexOf(CommandNames.CompressApiScanFindings);
        var correlateIdx = preset.IndexOf(CommandNames.CorrelateFindings);
        var loadSkillsIdx = preset.IndexOf(CommandNames.LoadSkills);

        compressIdx.Should().BeGreaterThanOrEqualTo(0);
        correlateIdx.Should().Be(compressIdx + 1);
        loadSkillsIdx.Should().Be(correlateIdx + 1);
    }
}
