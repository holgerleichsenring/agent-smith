using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

public sealed class EvidenceModeRenderingTests
{
    [Fact]
    public void AnalyzedFromSource_DisplayLocationPrefersFileLine()
    {
        var o = ObservationFactory.Make("HIGH", "src/Ctrl.cs", 42, "X", "Y", 80,
            apiPath: "POST /api/x",
            evidence: EvidenceMode.AnalyzedFromSource);
        o.DisplayLocation.Should().Be("src/Ctrl.cs:42");
    }

    [Fact]
    public void Confirmed_DisplayLocationPrefersApiPath()
    {
        var o = ObservationFactory.Make("HIGH", "", 0, "X", "Y", 80,
            apiPath: "POST /api/x",
            evidence: EvidenceMode.Confirmed);
        o.DisplayLocation.Should().Be("POST /api/x");
    }

    [Fact]
    public void Markdown_RendersThreeEvidenceLabels()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/A.cs", 10, "Source", "Desc", 80, evidence: EvidenceMode.AnalyzedFromSource),
            ObservationFactory.Make("MEDIUM", "", 0, "Probe", "Desc", 70, apiPath: "POST /x", evidence: EvidenceMode.Confirmed),
            ObservationFactory.Make("LOW", "", 0, "Schema", "Desc", 60, apiPath: "GET /x", evidence: EvidenceMode.Potential),
        };
        var md = MarkdownOutputStrategy.BuildMarkdown(observations);
        md.Should().Contain("analyzed from source");
        md.Should().Contain("confirmed (HTTP probe)");
        md.Should().Contain("potential (schema/pattern)");
    }

    [Fact]
    public void Sarif_IncludesEvidenceModeProperty()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/A.cs", 10, "Source", "Desc", 80, evidence: EvidenceMode.AnalyzedFromSource),
        };
        var sarif = SarifOutputStrategy.BuildSarifDocument(observations).ToJsonString();
        sarif.Should().Contain("evidence_mode").And.Contain("analyzed_from_source");
    }
}
