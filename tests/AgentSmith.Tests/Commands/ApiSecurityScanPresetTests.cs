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
    public void ApiSecurityScan_LoadSkillsImmediatelyAfterCompress()
    {
        var preset = PipelinePresets.ApiSecurityScan.ToList();
        var compressIdx = preset.IndexOf(CommandNames.CompressApiScanFindings);
        var loadSkillsIdx = preset.IndexOf(CommandNames.LoadSkills);

        compressIdx.Should().BeGreaterThanOrEqualTo(0);
        loadSkillsIdx.Should().Be(compressIdx + 1);
    }
}
