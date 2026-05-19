using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

public sealed class NucleiTopSelectorTests
{
    private readonly NucleiTopSelector _selector = new();

    private static NucleiFinding F(string severity, string url = "https://t/x", string id = "tpl") =>
        new(TemplateId: id, Name: "n", Severity: severity, MatchedUrl: url,
            Description: null, Reference: null);

    [Fact]
    public void SelectTop_Null_ReturnsEmpty()
    {
        _selector.SelectTop(null).Should().BeEmpty();
    }

    [Fact]
    public void SelectTop_Empty_ReturnsEmpty()
    {
        _selector.SelectTop(Array.Empty<NucleiFinding>()).Should().BeEmpty();
    }

    [Fact]
    public void SelectTop_BeyondCap_TakesTopCap()
    {
        var findings = Enumerable.Range(0, 50).Select(i => F("high", url: $"https://t/{i:00}")).ToArray();

        var top = _selector.SelectTop(findings);

        top.Should().HaveCount(20);
    }

    [Fact]
    public void SelectTop_SortsBySeverityDescending()
    {
        var findings = new[] { F("low"), F("critical"), F("info"), F("medium"), F("high") };

        var top = _selector.SelectTop(findings);

        top.Select(t => t.Severity).Should().ContainInOrder("critical", "high", "medium", "low", "info");
    }

    [Fact]
    public void SelectTop_DeterministicForSameInput()
    {
        var findings = new[] { F("high", "https://t/b"), F("high", "https://t/a"), F("high", "https://t/c") };

        _selector.SelectTop(findings).Should().BeEquivalentTo(
            _selector.SelectTop(findings), opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void SelectTop_UnknownSeverity_RanksLowest()
    {
        var findings = new[] { F("???"), F("low"), F("info") };

        var top = _selector.SelectTop(findings);

        top.Last().Severity.Should().Be("???");
    }
}

public sealed class ZapTopSelectorTests
{
    private readonly ZapTopSelector _selector = new();

    private static ZapFinding F(string risk, string url = "https://t/x", string id = "1") =>
        new(AlertRef: id, Name: "n", RiskDescription: risk, Confidence: "high",
            Url: url, Description: "d", Solution: null, CweId: null, WascId: null, Count: 1);

    [Fact]
    public void SelectTop_AllUnderCap_TakesAll()
    {
        var findings = new[] { F("high"), F("medium"), F("low") };

        _selector.SelectTop(findings).Should().HaveCount(3);
    }

    [Fact]
    public void SelectTop_SortsByRiskDescending()
    {
        var findings = new[] { F("low"), F("high"), F("medium") };

        _selector.SelectTop(findings).Select(t => t.RiskDescription)
            .Should().ContainInOrder("high", "medium", "low");
    }

    [Fact]
    public void SelectTop_BeyondCap_Caps()
    {
        var findings = Enumerable.Range(0, 50).Select(i => F("medium", url: $"https://t/{i:00}")).ToArray();

        _selector.SelectTop(findings).Should().HaveCount(30);
    }
}

public sealed class SpectralTopSelectorTests
{
    private readonly SpectralTopSelector _selector = new();

    private static SpectralFinding F(string severity, string code = "rule", string path = "$.x") =>
        new(Code: code, Message: "m", Path: path, Severity: severity, Line: 1);

    [Fact]
    public void SelectTop_WarningsOnly_ReturnsEmpty()
    {
        var findings = new[] { F("warn"), F("warn"), F("info") };

        _selector.SelectTop(findings).Should().BeEmpty();
    }

    [Fact]
    public void SelectTop_ErrorsOnly_TakesErrors()
    {
        var findings = new[] { F("error", "rule1"), F("error", "rule2") };

        _selector.SelectTop(findings).Should().HaveCount(2);
    }

    [Fact]
    public void SelectTop_RuleCluster_LimitsToThreeInstancesPerRule()
    {
        var findings = Enumerable.Range(0, 10)
            .Select(i => F("error", code: "chatty", path: $"$.{i}")).ToArray();

        var top = _selector.SelectTop(findings);

        top.Should().HaveCount(3);
    }

    [Fact]
    public void SelectTop_MultipleRules_BiggerClusterFirst()
    {
        var findings = new[]
        {
            F("error", "small"),
            F("error", "big"), F("error", "big"), F("error", "big"),
        };

        var top = _selector.SelectTop(findings);

        top[0].Code.Should().Be("big");
    }

    [Fact]
    public void SelectTop_TotalCap_Honored()
    {
        var findings = Enumerable.Range(0, 50)
            .Select(i => F("error", code: $"rule{i:00}", path: "$.x")).ToArray();

        _selector.SelectTop(findings).Should().HaveCount(30);
    }
}
