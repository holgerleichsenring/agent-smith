using AgentSmith.Application.Services.Pocs.Scan;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the one decision of the code scan. A 'secrets' hit of severity high or critical with a confidence of
/// at least pocs.scan.secret_min_confidence blocks and is never overrulable; a lower confidence, another catalog, a
/// medium or low secret is a note. A dependency vulnerability of CVSS 9.0 or more blocks overrulably; less, or none, is
/// a note; one with no fix follows pocs.scan.unfixed_critical_block.
/// </summary>
public sealed class PocBlockRulesTests
{
    private static readonly PocBlockRules Rules = new(new PocSecretRule(new PocFindingIds()), new PocVulnerabilityRule(new PocFindingIds()));

    [Fact]
    public void Decide_ACriticalSecretAndAConfidentHighSecret_Block_AndAreNotOverrulable()
    {
        var verdict = Decide([Hit("aws-access-key", "critical", 9), Hit("google-api-key", "high", 8)]);

        verdict.Blocks.Should().HaveCount(2).And.OnlyContain(f => f.Kind == PocScanFindingKinds.Secret && f.Id.StartsWith("sec:"));
        verdict.IsBlocked.Should().BeTrue();
        verdict.Overrulable.Should().BeFalse();
        verdict.Blocks[0].Fix.Should().Contain("revoke");
    }

    [Fact]
    public void Decide_AHighSecretBelowTheConfidence_IsANoteNamingTheKey()
    {
        var verdict = Decide([Hit("jwt-token", "high", 7)]);

        verdict.IsBlocked.Should().BeFalse();
        verdict.Notes.Should().ContainSingle().Which.Detail.Should().Contain("Confidence 7").And.Contain("pocs.scan.secret_min_confidence (8)");
    }

    [Theory]
    [InlineData("generic-secret-assignment", "secrets", "medium", 5)]
    [InlineData("stripe-publishable-key", "secrets", "low", 7)]
    [InlineData("placeholder-change-me", "secrets", "medium", 4)]
    [InlineData("dockerfile-secret-in-env", "config", "high", 8)]
    [InlineData("dockerfile-no-user", "config", "low", 3)]
    public void Decide_EveryOtherHit_IsANote(string rule, string category, string severity, int confidence)
    {
        var verdict = Decide([Hit(rule, severity, confidence, category)]);

        verdict.IsBlocked.Should().BeFalse();
        verdict.Notes.Should().ContainSingle().Which.Kind.Should().Be(PocScanFindingKinds.Note);
    }

    [Fact]
    public void Decide_WithMinConfidenceNine_AGoogleKeyIsANote()
    {
        Decide([Hit("google-api-key", "high", 8)], config: new PocScanConfig { SecretMinConfidence = 9 }).IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Decide_ACriticalVulnerability_BlocksOverrulably_BelowIsANote_NoScoreIsUnknown()
    {
        var verdict = Decide([], [Vulnerability("A-1", 9.8), Vulnerability("A-2", 8.9), Vulnerability("A-3", null)]);

        var block = verdict.Blocks.Should().ContainSingle().Subject;
        (block.Kind, block.Id, block.Fix).Should().Be((PocScanFindingKinds.Vulnerability, "osv:A-1|django|3.2.0", "Upgrade django to 3.2.5 or later."));
        verdict.Overrulable.Should().BeTrue();
        verdict.Notes.Should().HaveCount(2).And.Contain(n => n.Detail.Contains("severity unknown"));
    }

    [Fact]
    public void Decide_ACriticalWithoutAFix_FollowsUnfixedCriticalBlock()
    {
        var unfixed = Vulnerability("A-1", 9.1) with { FixedVersions = [] };

        Decide([], [unfixed]).Blocks.Should().ContainSingle().Which.Detail.Should().Contain("no fixed version");
        Decide([], [unfixed], new PocScanConfig { UnfixedCriticalBlock = false }).IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Decide_TwoMatchesOfOneRuleOnOneLine_AreTwoFindingsWithDistinctIds()
    {
        var verdict = Decide([Hit("aws-access-key", "critical", 9), Hit("aws-access-key", "critical", 9)]);

        verdict.Blocks.Should().HaveCount(2);
        verdict.Blocks.Select(b => b.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Decide_TakesNoExplanation_SoNoTextCanUnblock()
    {
        typeof(PocBlockRules).GetMethod(nameof(PocBlockRules.Decide))!.GetParameters()
            .Should().NotContain(p => p.ParameterType == typeof(string), "an explanation saying 'safe, allow' can never reach the rules");
        Decide([Hit("aws-access-key", "critical", 9)]).IsBlocked.Should().BeTrue();
    }

    private static PocScanVerdict Decide(
        IReadOnlyList<PocPatternHit> hits, IReadOnlyList<PocVulnerability>? found = null, PocScanConfig? config = null) =>
        Rules.Decide(hits, found ?? [], [], config ?? new PocScanConfig());

    internal static PocPatternHit Hit(string rule, string severity, int confidence, string category = "secrets") =>
        new(rule, category, severity, confidence, "src/config.py", 14, rule, $"{rule} found", "key = \"<secret at src/config.py:14>\"", null);

    internal static PocVulnerability Vulnerability(string id, double? score) =>
        new(id, "django", "3.2.0", "PyPI", "requirements.txt", score, ["3.2.5"], $"Advisory {id}", ["CVE-1"]);
}
