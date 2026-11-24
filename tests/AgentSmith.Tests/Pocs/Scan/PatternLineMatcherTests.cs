using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>2026-11-23-933dc: the shared per-line match returns every match with index and length, or each pattern's first
/// (the security scan's reading); a timeout keeps what was found and is reported.</summary>
public sealed class PatternLineMatcherTests
{
    private static readonly CompiledPattern Aws = new(new Regex("AKIA[0-9A-Z]{4}", RegexOptions.None, TimeSpan.FromMilliseconds(500)),
        new PatternDefinition("aws", "secrets", "AKIA[0-9A-Z]{4}", "critical", 9, "t", "d", null));

    [Fact]
    public void Match_ReturnsEveryMatch_OrTheFirstOnly()
    {
        var line = "a AKIAABCD b AKIAWXYZ";

        new PatternLineMatcher().Match(line, [Aws]).Select(m => (m.Index, m.Length)).Should().Equal((2, 8), (13, 8));
        new PatternLineMatcher().Match(line, [Aws], firstOnly: true).Should().ContainSingle().Which.Value.Should().Be("AKIAABCD");
    }

    [Fact]
    public void Match_ATimeout_IsReportedAndTheOtherPatternsStillMatch()
    {
        var slow = new CompiledPattern(new Regex("^(a+)+$", RegexOptions.None, TimeSpan.FromMilliseconds(50)),
            new PatternDefinition("slow", "injection", "^(a+)+$", "high", 5, "t", "d", null));
        var timedOut = new List<string>();

        var matches = new PatternLineMatcher().Match(new string('a', 5000) + "! AKIAABCD", [slow, Aws], p => timedOut.Add(p.Definition.Id));

        timedOut.Should().Equal("slow");
        matches.Should().ContainSingle().Which.Pattern.Should().Be(Aws);
    }
}
