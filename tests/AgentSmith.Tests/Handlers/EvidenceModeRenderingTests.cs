using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Output;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

public sealed class EvidenceModeRenderingTests
{
    [Fact]
    public void AnalyzedFromSource_DisplayLocationPrefersFileLine()
    {
        var f = new Finding(
            Severity: "high", File: "src/Ctrl.cs", StartLine: 42, EndLine: null,
            Title: "X", Description: "Y", Confidence: 80,
            ApiPath: "POST /api/x",
            EvidenceMode: EvidenceMode.AnalyzedFromSource);
        f.DisplayLocation.Should().Be("src/Ctrl.cs:42");
    }

    [Fact]
    public void Confirmed_DisplayLocationPrefersApiPath()
    {
        var f = new Finding(
            Severity: "high", File: "", StartLine: 0, EndLine: null,
            Title: "X", Description: "Y", Confidence: 80,
            ApiPath: "POST /api/x",
            EvidenceMode: EvidenceMode.Confirmed);
        f.DisplayLocation.Should().Be("POST /api/x");
    }

    [Fact]
    public void Markdown_RendersThreeEvidenceLabels()
    {
        var findings = new List<Finding>
        {
            new("high", "src/A.cs", 10, null, "Source", "Desc", 80, EvidenceMode: EvidenceMode.AnalyzedFromSource),
            new("medium", "", 0, null, "Probe", "Desc", 70, ApiPath: "POST /x", EvidenceMode: EvidenceMode.Confirmed),
            new("low", "", 0, null, "Schema", "Desc", 60, ApiPath: "GET /x", EvidenceMode: EvidenceMode.Potential),
        };
        var md = MarkdownOutputStrategy.BuildMarkdown(findings);
        md.Should().Contain("analyzed from source");
        md.Should().Contain("confirmed (HTTP probe)");
        md.Should().Contain("potential (schema/pattern)");
    }

    [Fact]
    public void Sarif_IncludesEvidenceModeProperty()
    {
        var findings = new List<Finding>
        {
            new("high", "src/A.cs", 10, null, "Source", "Desc", 80, EvidenceMode: EvidenceMode.AnalyzedFromSource),
        };
        var sarif = SarifOutputStrategy.BuildSarifDocument(findings).ToJsonString();
        sarif.Should().Contain("evidence_mode").And.Contain("analyzed_from_source");
    }
}
