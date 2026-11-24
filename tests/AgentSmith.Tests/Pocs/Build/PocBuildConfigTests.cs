using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Build;

/// <summary>2026-11-23-933dd: the build block's values, the pinned Trivy image and the wall-time finding.</summary>
public sealed class PocBuildConfigTests
{
    [Fact]
    public void Problems_ATagOnlyTrivyImage_IsRefusedNamingTheKey() =>
        new PocScanConfig { TrivyImage = "aquasec/trivy:0.58.1" }.Problems(false).Should().ContainSingle()
            .Which.Should().Contain(PocScanConfig.TrivyImageKey).And.Contain("by digest");

    [Fact]
    public void Problems_ATimeoutOfZero_IsRefusedNamingTheKey() =>
        new PocBuildConfig { ScanTimeoutMinutes = 0 }.Problems("main").Should().ContainSingle()
            .Which.Should().Contain("pocs.targets[].build.scan_timeout_minutes of target 'main' is 0");

    [Fact]
    public void Problems_ASecretNameThatIsNoVariable_IsRefused() =>
        new PocBuildConfig { PushPasswordSecret = "push-password" }.Problems("main").Should().ContainSingle()
            .Which.Should().Contain("push_password_secret");

    [Fact]
    public void WallTime_TheDefaults_Pass() =>
        PocBuildWallTime.Findings(new AgentSmithConfig { Pocs = PocsConfigTests.Sound() }).Should().BeEmpty("23 x 60 + 300 = 1680 < 1800");

    [Fact]
    public void WallTime_ABuildOfThirtyMinutes_IsBlockingNamingBothSumsAndTheKeys()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Targets[0].Build.BuildTimeoutMinutes = 30;

        var finding = PocBuildWallTime.Findings(new AgentSmithConfig { Pocs = pocs }).Should().ContainSingle().Subject;

        finding.IsBlocking.Should().BeTrue();
        finding.Reason.Should().Contain("41 min = 2460 s").And.Contain("2760 s").And.Contain("build_timeout_minutes")
            .And.Contain("orchestrator.max_run_wall_time_seconds (1800 s)").And.Contain("target 'main'");
    }
}
