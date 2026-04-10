using AgentSmith.Infrastructure.Services.Zap;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class ZapSpawnerTests
{
    [Fact]
    public void ParseZapJson_ValidFindings_ParsesCorrectly()
    {
        var json = """
            {
              "site": [{
                "alerts": [
                  {
                    "alertRef": "10038",
                    "name": "Content Security Policy",
                    "riskdesc": "Medium (High)",
                    "confidence": "High",
                    "desc": "CSP header not set",
                    "solution": "Set CSP header",
                    "cweid": "693",
                    "wascid": "15",
                    "count": "3",
                    "instances": [{"uri": "https://api.example.com/"}]
                  },
                  {
                    "alertRef": "10096",
                    "name": "Timestamp Disclosure",
                    "riskdesc": "Low (Low)",
                    "confidence": "Low",
                    "desc": "Timestamp found in response",
                    "cweid": "200",
                    "wascid": "13",
                    "count": "1",
                    "instances": [{"uri": "https://api.example.com/health"}]
                  }
                ]
              }]
            }
            """;

        var findings = ZapSpawner.ParseZapJson(json);

        findings.Should().HaveCount(2);

        findings[0].AlertRef.Should().Be("10038");
        findings[0].Name.Should().Be("Content Security Policy");
        findings[0].RiskDescription.Should().Be("Medium");
        findings[0].Confidence.Should().Be("High");
        findings[0].Url.Should().Be("https://api.example.com/");
        findings[0].Description.Should().Be("CSP header not set");
        findings[0].Solution.Should().Be("Set CSP header");
        findings[0].CweId.Should().Be("693");
        findings[0].WascId.Should().Be("15");
        findings[0].Count.Should().Be(3);

        findings[1].AlertRef.Should().Be("10096");
        findings[1].Name.Should().Be("Timestamp Disclosure");
        findings[1].RiskDescription.Should().Be("Low");
        findings[1].Count.Should().Be(1);
    }

    [Fact]
    public void ParseZapJson_EmptyOutput_ReturnsEmpty()
    {
        var findings = ZapSpawner.ParseZapJson("");
        findings.Should().BeEmpty();
    }

    [Fact]
    public void ParseZapJson_NullOutput_ReturnsEmpty()
    {
        var findings = ZapSpawner.ParseZapJson(null!);
        findings.Should().BeEmpty();
    }

    [Fact]
    public void ParseZapJson_NoSiteProperty_ReturnsEmpty()
    {
        var json = """{"version": "2.14.0"}""";
        var findings = ZapSpawner.ParseZapJson(json);
        findings.Should().BeEmpty();
    }

    [Fact]
    public void ParseZapJson_EmptyAlerts_ReturnsEmpty()
    {
        var json = """{"site": [{"alerts": []}]}""";
        var findings = ZapSpawner.ParseZapJson(json);
        findings.Should().BeEmpty();
    }

    [Fact]
    public void ParseZapJson_MultipleSites_CollectsAll()
    {
        var json = """
            {
              "site": [
                {
                  "alerts": [{
                    "alertRef": "10038",
                    "name": "CSP Missing",
                    "riskdesc": "Medium (High)",
                    "confidence": "High",
                    "desc": "No CSP",
                    "count": "1",
                    "instances": [{"uri": "https://site1.example.com/"}]
                  }]
                },
                {
                  "alerts": [{
                    "alertRef": "10096",
                    "name": "Timestamp",
                    "riskdesc": "Low (Low)",
                    "confidence": "Low",
                    "desc": "Timestamp found",
                    "count": "2",
                    "instances": [{"uri": "https://site2.example.com/"}]
                  }]
                }
              ]
            }
            """;

        var findings = ZapSpawner.ParseZapJson(json);
        findings.Should().HaveCount(2);
        findings[0].Url.Should().Be("https://site1.example.com/");
        findings[1].Url.Should().Be("https://site2.example.com/");
    }

    [Fact]
    public void ParseZapJson_InvalidJson_ReturnsEmpty()
    {
        var findings = ZapSpawner.ParseZapJson("not valid json {{{");
        findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://localhost:5000", "https://host.docker.internal:5000")]
    [InlineData("https://127.0.0.1:8080", "https://host.docker.internal:8080")]
    [InlineData("https://api.example.com", "https://api.example.com")]
    public void RewriteLocalhostForDocker_ReplacesCorrectly(string input, string expected)
    {
        ZapSpawner.RewriteLocalhostForDocker(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Medium (High)", "Medium")]
    [InlineData("High (Medium)", "High")]
    [InlineData("Low (Low)", "Low")]
    [InlineData("Informational (Medium)", "Informational")]
    [InlineData("", "Informational")]
    [InlineData(null, "Informational")]
    public void ExtractRiskLevel_ParsesCorrectly(string? input, string expected)
    {
        ZapSpawner.ExtractRiskLevel(input!).Should().Be(expected);
    }

    [Fact]
    public void BuildArguments_Baseline_ReturnsCorrectArgs()
    {
        var inputFiles = new Dictionary<string, string>();
        var args = ZapSpawner.BuildArguments("baseline", "https://example.com", null, inputFiles);

        args.Should().StartWith(["zap-baseline.py", "-t", "https://example.com"]);
        args.Should().Contain("-J");
        args.Should().NotContain("--auto");
        inputFiles.Should().BeEmpty();
    }

    [Fact]
    public void BuildArguments_FullScan_ReturnsCorrectArgs()
    {
        var inputFiles = new Dictionary<string, string>();
        var args = ZapSpawner.BuildArguments("full-scan", "https://example.com", null, inputFiles);

        args.Should().StartWith(["zap-full-scan.py", "-t", "https://example.com"]);
        args.Should().NotContain("--auto");
    }

    [Fact]
    public void BuildArguments_ApiScan_WithSwagger_CopiesSpecViaInputFiles()
    {
        // Create a temp swagger file for this test
        var tempSwagger = Path.Combine(Path.GetTempPath(), $"test-swagger-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempSwagger, """{"openapi": "3.0.0"}""");

        try
        {
            var inputFiles = new Dictionary<string, string>();
            var args = ZapSpawner.BuildArguments("api-scan", "https://example.com", tempSwagger, inputFiles);

            args.Should().StartWith(["zap-api-scan.py"]);
            args.Should().Contain("-f");
            args.Should().Contain("openapi");
            inputFiles.Should().ContainKey("swagger.json");
        }
        finally
        {
            File.Delete(tempSwagger);
        }
    }

    [Fact]
    public void BuildArguments_ApiScan_WithoutSwagger_FallsBackToTargetUrl()
    {
        var inputFiles = new Dictionary<string, string>();
        var args = ZapSpawner.BuildArguments("api-scan", "https://example.com", null, inputFiles);

        args.Should().StartWith(["zap-api-scan.py", "-t", "https://example.com"]);
        inputFiles.Should().BeEmpty();
    }

    [Fact]
    public void BuildArguments_UnknownScanType_DefaultsToBaseline()
    {
        var inputFiles = new Dictionary<string, string>();
        var args = ZapSpawner.BuildArguments("unknown", "https://example.com", null, inputFiles);

        args.Should().StartWith(["zap-baseline.py"]);
    }
}
