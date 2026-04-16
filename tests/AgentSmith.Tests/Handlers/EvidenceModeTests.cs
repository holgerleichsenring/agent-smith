using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

public sealed class EvidenceModeTests
{
    [Fact]
    public void Finding_DefaultEvidenceMode_IsPotential()
    {
        var finding = new Finding("HIGH", "test.cs", 1, null, "Test", "Desc", 8);
        finding.EvidenceMode.Should().Be(EvidenceMode.Potential);
    }

    [Fact]
    public void Finding_ConfirmedEvidenceMode_IsConfirmed()
    {
        var finding = new Finding("HIGH", "test.cs", 1, null, "Test", "Desc", 8,
            EvidenceMode: EvidenceMode.Confirmed);
        finding.EvidenceMode.Should().Be(EvidenceMode.Confirmed);
    }

    [Fact]
    public void Finding_ApiPathWithConfirmedEvidence_HasCorrectDisplayLocation()
    {
        var finding = new Finding("HIGH", "", 0, null, "IDOR", "Cross-user access", 9,
            ApiPath: "GET /api/users/123",
            EvidenceMode: EvidenceMode.Confirmed);

        finding.DisplayLocation.Should().Be("GET /api/users/123");
        finding.EvidenceMode.Should().Be(EvidenceMode.Confirmed);
    }
}
