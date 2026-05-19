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
            Description: "x", Suggestion: null, Blocking: false,
            Severity: ObservationSeverity.Info, Confidence: 50,
            EvidenceMode: mode, File: file);

    [Fact]
    public void IsAnchored_AnalyzedFromSourceAndFileInReadSet_ReturnsTrue()
    {
        var observation = Make(file: "src/Program.cs");
        var readPaths = new[] { "src/Program.cs" };

        _validator.IsAnchored(observation, readPaths, "judge", NullLogger.Instance).Should().BeTrue();
    }

    [Fact]
    public void IsAnchored_AnalyzedFromSourceAndFileNotInReadSet_ReturnsFalse()
    {
        var observation = Make(file: "src/Hallucination.cs");
        var readPaths = new[] { "src/Program.cs" };

        _validator.IsAnchored(observation, readPaths, "judge", NullLogger.Instance).Should().BeFalse();
    }

    [Fact]
    public void IsAnchored_AnalyzedFromSourceAndNoFile_ReturnsFalse()
    {
        var observation = Make(file: null);
        var readPaths = new[] { "src/Program.cs" };

        _validator.IsAnchored(observation, readPaths, "judge", NullLogger.Instance).Should().BeFalse();
    }

    [Fact]
    public void IsAnchored_AnalyzedFromSourceAndEmptyFile_ReturnsFalse()
    {
        var observation = Make(file: "");
        var readPaths = new[] { "src/Program.cs" };

        _validator.IsAnchored(observation, readPaths, "judge", NullLogger.Instance).Should().BeFalse();
    }

    [Fact]
    public void IsAnchored_PotentialEvidence_ReturnsTrueRegardlessOfFile()
    {
        var observation = Make(EvidenceMode.Potential, file: "src/anywhere.cs");
        var readPaths = Array.Empty<string>();

        _validator.IsAnchored(observation, readPaths, "judge", NullLogger.Instance).Should().BeTrue();
    }

    [Fact]
    public void IsAnchored_ConfirmedEvidence_ReturnsTrueRegardlessOfFile()
    {
        var observation = Make(EvidenceMode.Confirmed, file: null);
        var readPaths = Array.Empty<string>();

        _validator.IsAnchored(observation, readPaths, "judge", NullLogger.Instance).Should().BeTrue();
    }

    [Fact]
    public void IsAnchored_NoReadPathsProvided_PassesEvenForAnalyzedFromSource()
    {
        // When the runtime layer does not provide readPaths (e.g. legacy tests
        // that bypass the runtime), the validator does not enforce the rule.
        var observation = Make(file: "src/Anything.cs");

        _validator.IsAnchored(observation, readPaths: null, "judge", NullLogger.Instance).Should().BeTrue();
    }

    [Fact]
    public void IsAnchored_CaseInsensitiveFileMatch()
    {
        var observation = Make(file: "SRC/PROGRAM.CS");
        var readPaths = new[] { "src/Program.cs" };

        _validator.IsAnchored(observation, readPaths, "judge", NullLogger.Instance).Should().BeTrue();
    }
}
