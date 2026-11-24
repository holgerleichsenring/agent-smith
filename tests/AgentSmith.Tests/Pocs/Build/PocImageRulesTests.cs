using AgentSmith.Application.Services.Pocs.Build;
using AgentSmith.Application.Services.Pocs.Scan;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Pocs.Build;

/// <summary>2026-11-23-933dd: the image stage's decision — CVSS v3 9.0 and up blocks whatever Trivy's label says, no
/// score is a note, the unfixed rule as at the code stage, secrets by the mapped catalog confidence.</summary>
public sealed class PocImageRulesTests
{
    private static PocScanVerdict Decide(PocScanConfig? config = null, params string[] entries)
    {
        var (parsed, failure) = new TrivyReportParser().Parse("[" + string.Join(',', entries) + "]");
        failure.Should().BeNull();
        var ids = new PocFindingIds();
        var confidences = new Mock<IPocSecretConfidences>();
        confidences.Setup(c => c.Read()).Returns((PocBuildRig.Confidences(), null));
        var (confidenceOf, _) = new PocTrivySecretRules(confidences.Object).Read();
        return new PocImageRules(new PocVulnerabilityRule(ids), new PocSecretRule(ids), ids)
            .Decide(parsed, new HashSet<string>(), confidenceOf, config ?? new PocScanConfig());
    }

    [Fact]
    public void Decide_ScoreDecides_NotTrivysLabel()
    {
        var verdict = Decide(null, PocBuildRig.Vulnerability("CVE-1", 9.8, severity: "LOW"), PocBuildRig.Vulnerability("CVE-2", 7.5, severity: "CRITICAL"),
            """{"kind":"vulnerability","id":"CVE-3","package":"p","installed":"1","fixed":"","severity":"CRITICAL","scores":[],"title":"t","target":"x"}""");

        verdict.Blocks.Should().ContainSingle().Which.Id.Should().Be("trivy:CVE-1|openssl|1.0");
        verdict.Notes.Should().HaveCount(2).And.Contain(n => n.Detail.Contains("severity unknown"));
    }

    [Fact]
    public void Decide_AnUnfixedCritical_BlocksWithTheRuleOn_AndIsANoteWithItOff()
    {
        var unfixed = PocBuildRig.Vulnerability("CVE-1", 9.8, fixedVersion: "");

        Decide(null, unfixed).Blocks.Should().ContainSingle();
        Decide(new PocScanConfig { UnfixedCriticalBlock = false }, unfixed).Notes.Should().ContainSingle()
            .Which.Detail.Should().Contain(PocScanConfig.UnfixedCriticalBlockKey);
    }

    [Fact]
    public void Decide_AHighPrivateKeyBlocksUnoverrulably_AJwtAndAnUnmappedRuleAreNotes()
    {
        var verdict = Decide(null, PocBuildRig.Secret("private-key"), PocBuildRig.Secret("jwt-token"), PocBuildRig.Secret("dockerconfig-secret", "CRITICAL"));

        verdict.Blocks.Should().ContainSingle().Which.Should().Match<PocScanFinding>(f => f.Kind == PocScanFindingKinds.Secret && f.Id.StartsWith("sec:"));
        verdict.Overrulable.Should().BeFalse();
        verdict.Notes.Should().HaveCount(2).And.OnlyContain(n => n.Detail.Contains(PocScanConfig.SecretMinConfidenceKey));
    }

    [Fact]
    public void Decide_ACleanReport_Passes() => Decide().IsBlocked.Should().BeFalse();

    [Fact]
    public void Parse_NotAnArray_IsAFailure() =>
        new TrivyReportParser().Parse("{}").Failure.Should().Contain("not a JSON array");
}
