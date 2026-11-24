using AgentSmith.Application.Services.Pocs.Scan;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>2026-11-23-933dc: osv-scanner's JSON report as one vulnerability per advisory group and package — the group's
/// ordinal-lowest id, its max_severity, the fixed versions of the package's ranges, and the other ids as aliases.</summary>
public sealed class OsvReportParserTests
{
    private const string Prefix = "/work/lockfiles/";

    [Fact]
    public void Parse_AGroupWithScoreAndFix_ParsesSeverityFixAndAliases()
    {
        var found = new OsvReportParser().Parse(Report("""["PYSEC-1", "GHSA-b", "CVE-a"]""", "\"9.8\""), Prefix)!;

        var v = found.Should().ContainSingle().Subject;
        (v.AdvisoryId, v.Package, v.Version, v.File, v.Score).Should().Be(("CVE-a", "django", "3.2.0", "app/requirements.txt", 9.8));
        v.FixedVersions.Should().Equal("3.1.13", "3.2.5");
        v.Aliases.Should().Contain(["GHSA-b", "PYSEC-1", "BIT-1"]).And.NotContain("CVE-a");
        v.Title.Should().Be("SQL injection");
    }

    [Theory]
    [InlineData("""["PYSEC-1", "GHSA-b", "CVE-a"]""")]
    [InlineData("""["GHSA-b", "CVE-a", "PYSEC-1"]""")]
    public void Parse_TheGroupsId_IsItsOrdinalLowestMember_InAnyOrder(string ids) =>
        new OsvReportParser().Parse(Report(ids, "\"9.8\""), Prefix)!.Single().AdvisoryId.Should().Be("CVE-a");

    [Fact]
    public void Parse_NoSeverity_IsANullScore_AndGarbageIsNull()
    {
        new OsvReportParser().Parse(Report("""["PYSEC-1"]""", "null"), Prefix)!.Single().Score.Should().BeNull();
        new OsvReportParser().Parse("{not json", Prefix).Should().BeNull();
        new OsvReportParser().Parse("""{"results": []}""", Prefix).Should().BeEmpty();
    }

    private static string Report(string ids, string severity) => $$"""
        {"results": [{"source": {"path": "/work/lockfiles/app/requirements.txt", "type": "lockfile"},
          "packages": [{"package": {"name": "django", "version": "3.2.0", "ecosystem": "PyPI"},
            "vulnerabilities": [{"id": "PYSEC-1", "summary": "SQL injection", "details": "long text",
              "affected": [{"package": {"name": "django", "ecosystem": "PyPI"},
                "ranges": [{"type": "ECOSYSTEM", "events": [{"introduced": "3.1"}, {"fixed": "3.1.13"}, {"introduced": "3.2"}, {"fixed": "3.2.5"}]}]}]}],
            "groups": [{"ids": {{ids}}, "aliases": ["BIT-1", "CVE-a", "GHSA-b", "PYSEC-1"], "max_severity": {{severity}}}]}]}]}
        """;
}
