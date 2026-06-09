using System.Text.Json;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

public sealed class ObservationBusProjectorTests
{
    private readonly ObservationBusProjector _projector = new();

    private static SkillObservation Make(
        int id = 1,
        EvidenceMode mode = EvidenceMode.AnalyzedFromSource,
        string? file = "src/Program.cs",
        int startLine = 42,
        string? apiPath = null,
        string? schemaName = null,
        string description = "a finding",
        string? suggestion = "fix it",
        string? rationale = "long-form reason",
        string? details = "long-form details") =>
        new(Id: id, Role: "judge", Concern: ObservationConcern.Security,
            Description: description, Suggestion: suggestion!, Blocking: false,
            Severity: ObservationSeverity.High, Confidence: 80,
            Rationale: rationale, Details: details,
            EvidenceMode: mode, File: file, StartLine: startLine,
            ApiPath: apiPath, SchemaName: schemaName);

    [Fact]
    public void Project_EmptyList_ReturnsEmptyJsonArray()
    {
        _projector.Project(Array.Empty<SkillObservation>()).Should().Be("[]");
    }

    [Fact]
    public void Project_AnalyzedFromSourceObservation_IncludesFileAndStartLine()
    {
        var output = _projector.Project(new[] { Make(file: "src/Program.cs", startLine: 42) });

        var parsed = JsonDocument.Parse(output);
        parsed.RootElement[0].GetProperty("file").GetString().Should().Be("src/Program.cs");
        parsed.RootElement[0].GetProperty("start_line").GetInt32().Should().Be(42);
    }

    [Fact]
    public void Project_OmitsRationaleSuggestionDetails()
    {
        var output = _projector.Project(new[] { Make() });

        output.Should().NotContain("rationale").And.NotContain("suggestion").And.NotContain("details");
    }

    [Fact]
    public void Project_EvidenceMode_RendersAsSnakeCase()
    {
        var output = _projector.Project(new[] { Make(mode: EvidenceMode.AnalyzedFromSource) });

        output.Should().Contain("\"evidence_mode\":\"analyzed_from_source\"");
    }

    [Fact]
    public void Project_PotentialObservationWithApiPath_KeepsApiPath()
    {
        var output = _projector.Project(new[]
        {
            Make(mode: EvidenceMode.Potential, file: null, apiPath: "GET /api/users/{id}")
        });

        var parsed = JsonDocument.Parse(output);
        parsed.RootElement[0].GetProperty("api_path").GetString().Should().Be("GET /api/users/{id}");
    }

    [Fact]
    public void Project_EmptyFile_OmittedFromOutput()
    {
        var output = _projector.Project(new[] { Make(file: "", startLine: 0) });

        output.Should().NotContain("\"file\":\"\"");
        // start_line == 0 is also omitted (the runtime convention for "no line")
        output.Should().NotContain("\"start_line\":0");
    }

    [Fact]
    public void Project_PreservesId()
    {
        var output = _projector.Project(new[] { Make(id: 17) });

        var parsed = JsonDocument.Parse(output);
        parsed.RootElement[0].GetProperty("id").GetInt32().Should().Be(17);
    }

    [Fact]
    public void Project_MultipleObservations_RendersAsJsonArray()
    {
        var output = _projector.Project(new[] { Make(id: 1), Make(id: 2), Make(id: 3) });

        var parsed = JsonDocument.Parse(output);
        parsed.RootElement.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public void Project_DeterministicForSameInput()
    {
        var observations = new[] { Make(id: 1), Make(id: 2) };

        _projector.Project(observations).Should().Be(_projector.Project(observations));
    }
}
