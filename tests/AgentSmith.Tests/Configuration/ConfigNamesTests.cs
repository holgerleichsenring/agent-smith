using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// p0515: the one rule for matching a configured name, and the collision detector built on
/// it. The rule is ordinal-ignore-case — not the current culture (a Turkish container answers
/// that 'I' and 'i' are different letters) and not the invariant culture (whose comparison is
/// linguistic and equates names separated by a soft hyphen).
/// </summary>
public sealed class ConfigNamesTests
{
    [Fact]
    public void Comparer_TwoSpellingsOfOneName_AreOneKey()
    {
        var map = new Dictionary<string, int>(ConfigNames.Comparer) { ["Service.Api"] = 1 };

        map.ContainsKey("SERVICE.API").Should().BeTrue();
        map.ContainsKey("service.api").Should().BeTrue();
    }

    [Fact]
    public void Comparer_ADottedI_IsNotFoldedByACulture()
    {
        // The trap the culture-sensitive comparers set: under a Turkish culture 'I' and 'i'
        // are different letters, so the same configuration would resolve differently
        // depending on the container's LANG.
        ConfigNames.AreSame("INIT", "init").Should().BeTrue();
        ConfigNames.Comparison.Should().Be(StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Grouping_ByTheComparer_AgreesWithTheComparer_OnTheCodePointsUppercasingGetsWrong()
    {
        // The trap a second form of the rule sets: invariant uppercasing folds 'ſ' (U+017F) onto
        // 'S', ordinal-ignore-case does not. A detector keyed by an uppercased name would drop a
        // pair the catalogs keep apart, so grouping is done with the comparer itself.
        ConfigNames.AreSame("ſ", "S").Should().BeFalse();
        new[] { "ſ", "S" }.GroupBy(k => k, ConfigNames.Comparer).Should().HaveCount(2);
        new[] { "Demo", "DEMO" }.GroupBy(k => k, ConfigNames.Comparer).Should().HaveCount(1);
    }

    [Fact]
    public void KeyedByName_ACollidingPair_CollapsesInsteadOfThrowing()
    {
        // The dictionary copy constructor throws on a case collision, and a throw while
        // reading configuration is a dead process rather than a reported fault.
        var source = new Dictionary<string, int> { ["a"] = 1, ["A"] = 2 };

        var keyed = ConfigNames.KeyedByName(source);

        keyed.Should().ContainSingle();
        keyed.Comparer.Should().Be(ConfigNames.Comparer);
    }

    [Fact]
    public void Detect_TwoSpellingsOfOneKey_DropBothAndNameTheCatalog()
    {
        var findings = new List<StartupFinding>();

        var dropped = new CatalogKeyCollisions().Detect("repos", ["Api", "api", "other"], findings);

        dropped.Should().BeEquivalentTo(["Api", "api"]);
        var finding = findings.Should().ContainSingle().Subject;
        finding.Field.Should().Be("repos:Api");
        finding.Project.Should().BeNull();
        finding.Severity.Should().Be(StartupFindingSeverity.Blocking);
        finding.Reason.Should().Contain("'Api'").And.Contain("'api'");
    }

    [Fact]
    public void Detect_NoCollision_NamesNothing()
    {
        var findings = new List<StartupFinding>();

        var dropped = new CatalogKeyCollisions().Detect("agents", ["a", "b"], findings);

        dropped.Should().BeEmpty();
        findings.Should().BeEmpty();
    }
}
