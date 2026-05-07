using AgentSmith.Contracts.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

public sealed class EvidenceModeTests
{
    [Fact]
    public void Observation_DefaultEvidenceMode_IsPotential()
    {
        var obs = new SkillObservation(
            Id: 0, Role: "test",
            Concern: ObservationConcern.Security,
            Description: "Test", Suggestion: "",
            Blocking: false, Severity: ObservationSeverity.High, Confidence: 80);
        obs.EvidenceMode.Should().Be(EvidenceMode.Potential);
    }

    [Fact]
    public void Observation_ConfirmedEvidenceMode_IsConfirmed()
    {
        var obs = ObservationFactory.Make("HIGH", "test.cs", 1, "Test", "Desc", 80,
            evidence: EvidenceMode.Confirmed);
        obs.EvidenceMode.Should().Be(EvidenceMode.Confirmed);
    }

    [Fact]
    public void Observation_ApiPathWithConfirmedEvidence_HasCorrectDisplayLocation()
    {
        var obs = ObservationFactory.Make("HIGH", "", 0, "IDOR", "Cross-user access", 90,
            apiPath: "GET /api/users/123",
            evidence: EvidenceMode.Confirmed);

        obs.DisplayLocation.Should().Be("GET /api/users/123");
        obs.EvidenceMode.Should().Be(EvidenceMode.Confirmed);
    }
}
