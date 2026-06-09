using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

public sealed class SourceAnchorValidatorTests
{
    private readonly SourceAnchorValidator _validator = new();

    private static SkillObservation Make(
        EvidenceMode mode = EvidenceMode.AnalyzedFromSource,
        string? file = "src/Program.cs") =>
        new(Id: 1, Role: "judge", Concern: ObservationConcern.Security,
            Description: "x", Suggestion: null!, Blocking: false,
            Severity: ObservationSeverity.Info, Confidence: 50,
            EvidenceMode: mode, File: file);

    [Fact]
    public void EnforceAnchor_AnalyzedFromSourceAndFileInReadSet_ReturnsUnchanged()
    {
        var observation = Make(file: "src/Program.cs");
        var readPaths = new[] { "src/Program.cs" };

        var result = _validator.EnforceAnchor(observation, readPaths, "judge", NullLogger.Instance);

        result.EvidenceMode.Should().Be(EvidenceMode.AnalyzedFromSource);
        result.File.Should().Be("src/Program.cs");
    }

    [Fact]
    public void EnforceAnchor_AnalyzedFromSourceAndFileNotInReadSet_DowngradesToPotentialAndClearsFile()
    {
        var observation = Make(file: "src/Hallucination.cs");
        var readPaths = new[] { "src/Program.cs" };

        var result = _validator.EnforceAnchor(observation, readPaths, "judge", NullLogger.Instance);

        result.EvidenceMode.Should().Be(EvidenceMode.Potential);
        result.File.Should().BeNull();
        result.Description.Should().Be("x");  // signal preserved
    }

    [Fact]
    public void EnforceAnchor_AnalyzedFromSourceAndNoFile_DowngradesToPotential()
    {
        var observation = Make(file: null);
        var readPaths = new[] { "src/Program.cs" };

        var result = _validator.EnforceAnchor(observation, readPaths, "judge", NullLogger.Instance);

        result.EvidenceMode.Should().Be(EvidenceMode.Potential);
        result.File.Should().BeNull();
        result.Description.Should().Be("x");  // signal preserved
    }

    [Fact]
    public void EnforceAnchor_AnalyzedFromSourceAndEmptyFile_DowngradesToPotential()
    {
        var observation = Make(file: "");
        var readPaths = new[] { "src/Program.cs" };

        var result = _validator.EnforceAnchor(observation, readPaths, "judge", NullLogger.Instance);

        result.EvidenceMode.Should().Be(EvidenceMode.Potential);
        result.File.Should().BeNull();
    }

    [Fact]
    public void EnforceAnchor_PotentialEvidence_ReturnsUnchangedRegardlessOfFile()
    {
        var observation = Make(EvidenceMode.Potential, file: "src/anywhere.cs");
        var readPaths = Array.Empty<string>();

        var result = _validator.EnforceAnchor(observation, readPaths, "judge", NullLogger.Instance);

        result.EvidenceMode.Should().Be(EvidenceMode.Potential);
        result.File.Should().Be("src/anywhere.cs");
    }

    [Fact]
    public void EnforceAnchor_ConfirmedEvidence_ReturnsUnchangedRegardlessOfFile()
    {
        var observation = Make(EvidenceMode.Confirmed, file: null);
        var readPaths = Array.Empty<string>();

        var result = _validator.EnforceAnchor(observation, readPaths, "judge", NullLogger.Instance);

        result.EvidenceMode.Should().Be(EvidenceMode.Confirmed);
    }

    [Fact]
    public void EnforceAnchor_NoReadPathsProvided_PassesEvenForAnalyzedFromSource()
    {
        // When the runtime layer does not provide readPaths (e.g. legacy tests
        // that bypass the runtime), the validator does not enforce the rule
        // and returns the observation unchanged.
        var observation = Make(file: "src/Anything.cs");

        var result = _validator.EnforceAnchor(observation, readPaths: null, "judge", NullLogger.Instance);

        result.EvidenceMode.Should().Be(EvidenceMode.AnalyzedFromSource);
        result.File.Should().Be("src/Anything.cs");
    }

    [Fact]
    public void EnforceAnchor_CaseInsensitiveFileMatch()
    {
        var observation = Make(file: "SRC/PROGRAM.CS");
        var readPaths = new[] { "src/Program.cs" };

        var result = _validator.EnforceAnchor(observation, readPaths, "judge", NullLogger.Instance);

        result.EvidenceMode.Should().Be(EvidenceMode.AnalyzedFromSource);
    }
}
