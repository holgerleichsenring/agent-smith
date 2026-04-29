using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class ApiScanFindingsCompressorHeadersTests
{
    [Fact]
    public void BuildCategorySlices_CspFinding_GoesToHeadersSlice()
    {
        var n = new NucleiResult(
            [new NucleiFinding("missing-csp", "Missing CSP header", "medium", "https://api.test/x", null, null)],
            1, "");

        var slices = ApiScanFindingsCompressor.BuildCategorySlices(n, null, null);

        slices.Should().ContainKey("headers");
        slices["headers"].Should().Contain("Missing CSP");
        slices.GetValueOrDefault("runtime").Should().BeNullOrEmpty();
    }

    [Fact]
    public void BuildCategorySlices_HstsFinding_GoesToHeadersSlice()
    {
        var n = new NucleiResult(
            [new NucleiFinding("hsts-missing", "Strict-Transport-Security missing", "high", "https://api.test/x", null, null)],
            1, "");

        var slices = ApiScanFindingsCompressor.BuildCategorySlices(n, null, null);

        slices.Should().ContainKey("headers");
        slices["headers"].Should().Contain("Strict-Transport-Security");
    }

    [Fact]
    public void BuildCategorySlices_BolaFinding_StaysInRuntimeSlice()
    {
        var n = new NucleiResult(
            [new NucleiFinding("bola-test", "BOLA on /users", "critical", "https://api.test/users/1", null, null)],
            1, "");

        var slices = ApiScanFindingsCompressor.BuildCategorySlices(n, null, null);

        slices.Should().ContainKey("runtime");
        slices["runtime"].Should().Contain("BOLA");
        slices.GetValueOrDefault("headers").Should().BeNullOrEmpty();
    }

    [Fact]
    public void BuildCategorySlices_MixedFindings_SplitCorrectly()
    {
        var n = new NucleiResult(
            [
                new NucleiFinding("csp-missing", "CSP missing", "medium", "https://api.test/x", null, null),
                new NucleiFinding("bola-test", "BOLA risk", "high", "https://api.test/users/1", null, null),
            ],
            1, "");

        var slices = ApiScanFindingsCompressor.BuildCategorySlices(n, null, null);

        slices["headers"].Should().Contain("CSP missing");
        slices["headers"].Should().NotContain("BOLA");
        slices["runtime"].Should().Contain("BOLA");
        slices["runtime"].Should().NotContain("CSP missing");
    }
}
