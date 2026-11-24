using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933dc: pocs.scan and pocs.blocked_retention_days — defaults, and each refusal naming the value, the
/// reason and the key.</summary>
public sealed class PocScanConfigTests
{
    [Fact]
    public void Defaults_AreTheSpecsValues()
    {
        var pocs = new PocsConfig();

        (pocs.Scan.TimeoutSeconds, pocs.Scan.UnfixedCriticalBlock, pocs.Scan.SecretMinConfidence, pocs.BlockedRetentionDays)
            .Should().Be((300, true, 8, 7));
        pocs.Scan.OsvImage.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Problems_SecretMinConfidenceOutsideTheScale_IsRefusedNamingTheKey(int confidence)
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Scan.SecretMinConfidence = confidence;

        pocs.Problems().Should().ContainSingle().Which.Should().Contain("pocs.scan.secret_min_confidence")
            .And.Contain($"is {confidence}").And.Contain("the catalog rates pattern confidence from 1 to 10");
    }

    [Fact]
    public void Problems_ATagOnlyOsvImage_IsRefusedNamingTheKey()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Scan.OsvImage = "ghcr.io/google/osv-scanner:v2.2.4";

        pocs.Problems().Should().ContainSingle().Which.Should().Contain("pocs.scan.osv_image").And.Contain("by digest");
    }

    [Fact]
    public void Problems_EmptyImages_RunTheVerifiedDefaults_NoProblem()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Scan.OsvImage = "";
        pocs.Scan.TrivyImage = "";

        // 2026-11-23-933dg (operator ruling): an image is set only for an own mirror; empty is the pinned default.
        pocs.Problems().Should().BeEmpty();
        pocs.Scan.OsvImageInUse().Should().Be(PocScanConfig.DefaultOsvImage).And.Contain("@sha256:");
        pocs.Scan.TrivyImageInUse().Should().Be(PocScanConfig.DefaultTrivyImage).And.MatchRegex("^aquasec/trivy:[0-9.]+@sha256:[0-9a-f]{64}$");
        pocs.Scan.IsPinned().Should().BeTrue();
    }

    [Fact]
    public void Problems_TheDefaultsThemselves_PassTheirOwnPinRules()
    {
        var mirror = new PocScanConfig { OsvImage = PocScanConfig.DefaultOsvImage, TrivyImage = PocScanConfig.DefaultTrivyImage };

        mirror.Problems().Should().BeEmpty();
    }

    [Fact]
    public void Problems_ZeroRetentionOrTimeout_AreRefusedNamingTheKeys()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.BlockedRetentionDays = 0;
        pocs.Scan.TimeoutSeconds = 0;

        pocs.Problems().Should().HaveCount(2).And.Contain(p => p.StartsWith("pocs.blocked_retention_days is 0"))
            .And.Contain(p => p.StartsWith("pocs.scan.timeout_seconds is 0"));
    }
}
