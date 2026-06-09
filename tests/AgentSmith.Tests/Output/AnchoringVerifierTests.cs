using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Output;
using FluentAssertions;

namespace AgentSmith.Tests.Output;

public sealed class AnchoringVerifierTests
{
    private readonly AnchoringVerifier _verifier = new();

    private static SkillObservation Make(
        EvidenceMode mode = EvidenceMode.AnalyzedFromSource,
        string? file = "src/x.cs",
        string? apiPath = null,
        string? schemaName = null,
        string description = "x",
        string? category = null) =>
        new(Id: 1, Role: "judge", Concern: ObservationConcern.Security,
            Description: description, Suggestion: null!, Blocking: false,
            Severity: ObservationSeverity.Info, Confidence: 50,
            EvidenceMode: mode, File: file, ApiPath: apiPath,
            SchemaName: schemaName, Category: category);

    [Fact]
    public void Verify_EmptyList_ReturnsSingleNoFindingsPass()
    {
        var result = _verifier.Verify(Array.Empty<SkillObservation>());

        result.Should().HaveCount(1);
        result[0].Passed.Should().BeTrue();
        result[0].Detail.Should().Contain("no findings");
    }

    [Fact]
    public void Verify_AllAnchored_AnchoringAssertionPasses()
    {
        var observations = new[]
        {
            Make(file: "src/a.cs"),
            Make(mode: EvidenceMode.Potential, file: null, apiPath: "GET /api/x"),
            Make(mode: EvidenceMode.Confirmed, file: null, schemaName: "User"),
        };

        var result = _verifier.Verify(observations);

        result.Single(r => r.Name == "anchoring").Passed.Should().BeTrue();
    }

    [Fact]
    public void Verify_OrphanObservation_AnchoringAssertionFails()
    {
        var observations = new[]
        {
            Make(file: "src/a.cs"),
            Make(mode: EvidenceMode.Potential, file: null, apiPath: null, schemaName: null,
                description: "generic OWASP boilerplate"),
        };

        var result = _verifier.Verify(observations);

        result.Single(r => r.Name == "anchoring").Passed.Should().BeFalse();
        result.Single(r => r.Name == "anchoring").Detail.Should().Contain("1/2");
    }

    [Fact]
    public void Verify_ScannerTemplateIdInDescription_CountsAsAnchor()
    {
        var observations = new[]
        {
            Make(mode: EvidenceMode.Confirmed, file: null, apiPath: null, schemaName: null,
                description: "template_id: nuclei-templates/cves/2024-1234 — matched"),
        };

        var result = _verifier.Verify(observations);

        result.Single(r => r.Name == "anchoring").Passed.Should().BeTrue();
    }

    [Fact]
    public void Verify_AnalyzedFromSourceWithoutFile_SourceClaimsAssertionFails()
    {
        var observations = new[]
        {
            Make(mode: EvidenceMode.AnalyzedFromSource, file: null,
                apiPath: "/api/x", description: "no file but claims source"),
        };

        var result = _verifier.Verify(observations);

        // The anchoring assertion passes (api_path is present).
        result.Single(r => r.Name == "anchoring").Passed.Should().BeTrue();
        // But source-claims fails: claims source without a file.
        result.Single(r => r.Name == "source-claims").Passed.Should().BeFalse();
    }

    [Fact]
    public void Verify_ExecutionLimitObservations_NotCountedAsOrphan()
    {
        var observations = new[]
        {
            Make(file: "src/a.cs"),
            Make(mode: EvidenceMode.Confirmed, file: null, apiPath: null, schemaName: null,
                description: "cost cap exhausted", category: ExecutionLimitCategories.CostCapExhausted),
        };

        var result = _verifier.Verify(observations);

        result.Single(r => r.Name == "anchoring").Passed.Should().BeTrue();
    }
}
