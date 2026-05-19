using AgentSmith.Contracts.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

public sealed class ObservationParserTests
{
    private static readonly NullLogger Logger = NullLogger.Instance;
    private readonly AgentSmith.Application.Services.Handlers.ObservationParser _parser =
        TolerantJsonParserFactory.CreateObservation();

    [Fact]
    public void SkillObservation_IdAssignedByFramework_NotByLlm()
    {
        // LLM response includes no "id" field — framework assigns sequential IDs
        var json = """
            [
              { "concern": "security", "description": "SQL injection risk", "suggestion": "Use parameterized queries", "blocking": true, "severity": "high", "confidence": 90 },
              { "concern": "architecture", "description": "Missing abstraction", "suggestion": "Extract interface", "blocking": false, "severity": "medium", "confidence": 70 }
            ]
            """;

        var result = _parser.Parse(json, "architect", 1, Logger);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1);
        result[1].Id.Should().Be(2);
        result[0].Role.Should().Be("architect");
    }

    [Fact]
    public void Parse_ValidJsonObservations_StoresAllFields()
    {
        // Skills emit typed location fields directly (p0146d) — no regex post-pass.
        var json = """
            [
              {
                "concern": "security",
                "description": "Password in query string",
                "suggestion": "Move to POST body",
                "blocking": true,
                "severity": "high",
                "confidence": 95,
                "rationale": "OWASP A2:2021",
                "api_path": "POST /api/auth/login",
                "effort": "small"
              }
            ]
            """;

        var result = _parser.Parse(json, "security-reviewer", 10, Logger);

        result.Should().HaveCount(1);
        var obs = result[0];
        obs.Id.Should().Be(10);
        obs.Role.Should().Be("security-reviewer");
        obs.Concern.Should().Be(ObservationConcern.Security);
        obs.Description.Should().Contain("Password in query string");
        obs.Suggestion.Should().Contain("POST body");
        obs.Blocking.Should().BeTrue();
        obs.Severity.Should().Be(ObservationSeverity.High);
        obs.Confidence.Should().Be(95);
        obs.Rationale.Should().Be("OWASP A2:2021");
        obs.ApiPath.Should().Be("POST /api/auth/login");
        obs.Effort.Should().Be(ObservationEffort.Small);
    }

    [Fact]
    public void Parse_LegacyLocationStringIsIgnored_StructuredFieldsStayNull()
    {
        var json = """
            [
              { "concern": "security", "description": "Issue", "suggestion": "Fix",
                "blocking": false, "severity": "low", "confidence": 60,
                "location": "src/Foo.cs:42" }
            ]
            """;

        var result = _parser.Parse(json, "reviewer", 1, Logger);

        result.Should().HaveCount(1);
        result[0].File.Should().BeNull();
        result[0].StartLine.Should().Be(0);
        result[0].ApiPath.Should().BeNull();
        result[0].SchemaName.Should().BeNull();
    }

    [Fact]
    public void Parse_StructuredFileAndStartLine_StoredDirectly()
    {
        var json = """
            [
              { "concern": "correctness", "description": "Off-by-one",
                "suggestion": "Use Length-1", "blocking": false,
                "severity": "medium", "confidence": 80,
                "file": "src/Foo.cs", "start_line": 42, "end_line": 48 }
            ]
            """;

        var result = _parser.Parse(json, "reviewer", 1, Logger);

        result.Should().HaveCount(1);
        result[0].File.Should().Be("src/Foo.cs");
        result[0].StartLine.Should().Be(42);
        result[0].EndLine.Should().Be(48);
    }

    [Fact]
    public void Parse_InvalidJson_FallsBackToSingleObservation()
    {
        var freeText = "This is not JSON, just my thoughts on the architecture.";

        var result = _parser.Parse(freeText, "architect", 1, Logger);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(1);
        result[0].Role.Should().Be("architect");
        result[0].Concern.Should().Be(ObservationConcern.Correctness);
        result[0].Blocking.Should().BeFalse();
        result[0].Severity.Should().Be(ObservationSeverity.Info);
        result[0].Confidence.Should().Be(50);
        result[0].Description.Should().Contain("This is not JSON");
    }

    [Fact]
    public void Parse_PartialValidJson_TakesValidSkipsBroken()
    {
        var json = """
            [
              { "concern": "security", "description": "SQL injection", "suggestion": "Fix it", "blocking": true, "severity": "high", "confidence": 90 },
              { "concern": "architecture", "description": "", "suggestion": "N/A", "blocking": false, "severity": "low", "confidence": 50 },
              { "concern": "performance", "description": "N+1 query", "suggestion": "Add eager loading", "blocking": false, "severity": "medium", "confidence": 85 }
            ]
            """;

        var result = _parser.Parse(json, "reviewer", 1, Logger);

        result.Should().HaveCount(2);
        result[0].Concern.Should().Be(ObservationConcern.Security);
        result[1].Concern.Should().Be(ObservationConcern.Performance);
        result[0].Id.Should().Be(1);
        result[1].Id.Should().Be(2);
    }

    [Fact]
    public void Parse_JsonWithMarkdownFences_ExtractsCorrectly()
    {
        var json = """
            ```json
            [
              { "concern": "correctness", "description": "Missing null check", "suggestion": "Add guard", "blocking": true, "severity": "high", "confidence": 85 }
            ]
            ```
            """;

        var result = _parser.Parse(json, "reviewer", 1, Logger);

        result.Should().HaveCount(1);
        result[0].Description.Should().Contain("Missing null check");
    }

    [Fact]
    public void Parse_ConfidenceClampedTo0_100()
    {
        var json = """
            [
              { "concern": "security", "description": "Issue", "suggestion": "Fix", "blocking": false, "severity": "low", "confidence": 150 }
            ]
            """;

        var result = _parser.Parse(json, "reviewer", 1, Logger);

        result[0].Confidence.Should().Be(100);
    }

    [Fact]
    public void ObservationConcern_IsEnum_WithExpectedValues()
    {
        var values = Enum.GetValues<ObservationConcern>();

        values.Should().Contain(ObservationConcern.Correctness);
        values.Should().Contain(ObservationConcern.Architecture);
        values.Should().Contain(ObservationConcern.Performance);
        values.Should().Contain(ObservationConcern.Security);
        values.Should().Contain(ObservationConcern.Legal);
        values.Should().Contain(ObservationConcern.Compliance);
        values.Should().Contain(ObservationConcern.Risk);
        values.Should().HaveCount(7);
    }

    [Fact]
    public void ObservationLink_RelationshipValues_AreConstrained()
    {
        var values = Enum.GetValues<ObservationRelationship>();

        values.Should().Contain(ObservationRelationship.Duplicates);
        values.Should().Contain(ObservationRelationship.Contradicts);
        values.Should().Contain(ObservationRelationship.DependsOn);
        values.Should().Contain(ObservationRelationship.Extends);
        values.Should().HaveCount(4);
    }
}
