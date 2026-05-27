using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Output;

[Collection("ConsoleOut")]
public sealed class SummaryEvidenceBreakdownTests
{
    [Fact]
    public async Task SourceAnchoredFindings_RenderInDedicatedCodeFindingsSection()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "Program.cs", 15, "JWT misconfig", "", 90,
                evidence: EvidenceMode.AnalyzedFromSource),
            ObservationFactory.Make("MEDIUM", "Program.cs", 45, "CORS too permissive", "", 80,
                evidence: EvidenceMode.AnalyzedFromSource),
            ObservationFactory.Make("HIGH", "src/X.cs", 0, "Schema lint", "", 70,
                evidence: EvidenceMode.Potential, apiPath: "/users"),
        };

        var output = await CaptureSummaryAsync(observations);

        output.Should().Contain("Code Findings (2)");
        output.Should().Contain("Program.cs:15");
        output.Should().Contain("Program.cs:45");
        output.Should().Contain("JWT misconfig");
        output.Should().Contain("CORS too permissive");
    }

    [Fact]
    public async Task NoSourceAnchoredFindings_RendersEvidenceLineOnly_NoCodeFindingsHeader()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "", 0, "Schema lint", "", 70,
                evidence: EvidenceMode.Potential, apiPath: "/users"),
        };

        var output = await CaptureSummaryAsync(observations);

        output.Should().NotContain("Code Findings");
        output.Should().Contain("Evidence: 0 from source, 0 confirmed, 1 schema/inferred");
    }

    [Fact]
    public async Task MixedEvidence_TalliesAllThreeCategories()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "Program.cs", 15, "JWT", "", 90,
                evidence: EvidenceMode.AnalyzedFromSource),
            ObservationFactory.Make("HIGH", "", 0, "Probe hit", "", 85,
                evidence: EvidenceMode.Confirmed, apiPath: "/admin"),
            ObservationFactory.Make("MEDIUM", "", 0, "Schema lint", "", 70,
                evidence: EvidenceMode.Potential, apiPath: "/users"),
            ObservationFactory.Make("LOW", "", 0, "Schema lint", "", 60,
                evidence: EvidenceMode.Potential, apiPath: "/posts"),
        };

        var output = await CaptureSummaryAsync(observations);

        output.Should().Contain("Code Findings (1)");
        output.Should().Contain("Evidence: 1 from source, 1 confirmed, 2 schema/inferred");
    }

    [Fact]
    public async Task ParseFailureMetaObservation_FiltersOutOfFindingsTally_RendersInLimitsFooter()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "Program.cs", 15, "JWT", "", 90,
                evidence: EvidenceMode.AnalyzedFromSource),
            new(
                Id: 99, Role: "report-synthesizer",
                Concern: ObservationConcern.Correctness,
                Description: "Skill 'report-synthesizer' returned an empty response — no observations could be parsed.",
                Suggestion: "", Blocking: false,
                Severity: ObservationSeverity.Info, Confidence: 0,
                Category: ExecutionLimitCategories.ExecutionParseFailure),
        };

        var output = await CaptureSummaryAsync(observations);

        output.Should().Contain("Total: 1 findings");
        output.Should().Contain("Execution limits hit: 1");
        output.Should().Contain("[parse failure]");
    }

    private static async Task<string> CaptureSummaryAsync(IReadOnlyList<SkillObservation> observations)
    {
        var pipeline = new PipelineContext();
        var ctx = new OutputContext("test", null, observations, null, "./test-output", pipeline);
        var sut = new SummaryOutputStrategy(new AnchoringVerifier(), NullLogger<SummaryOutputStrategy>.Instance);
        var original = Console.Out;
        await using var capture = new StringWriter();
        Console.SetOut(capture);
        try { await sut.DeliverAsync(ctx); }
        finally { Console.SetOut(original); }
        return capture.ToString();
    }
}
