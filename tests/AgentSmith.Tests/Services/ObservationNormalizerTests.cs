using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ObservationNormalizerTests
{
    private readonly ObservationNormalizer _normalizer = new();

    [Fact]
    public void Normalize_LowConfidence_MigratesScale_1To10_Into_0To100()
    {
        var fields = Make(confidence: 5);

        var result = _normalizer.Normalize(fields, "tester", id: 1, new HashSet<string>(), NullLogger.Instance);

        result.Confidence.Should().Be(50, "5 on a 1-10 scale becomes 50 on a 0-100 scale");
    }

    [Fact]
    public void Normalize_ConfidenceAlreadyOnHundredScale_KeptVerbatim()
    {
        var fields = Make(confidence: 85);

        var result = _normalizer.Normalize(fields, "tester", id: 1, new HashSet<string>(), NullLogger.Instance);

        result.Confidence.Should().Be(85);
    }

    [Fact]
    public void Normalize_NegativeConfidence_ClampedToZero()
    {
        var fields = Make(confidence: -1);

        var result = _normalizer.Normalize(fields, "tester", id: 1, new HashSet<string>(), NullLogger.Instance);

        result.Confidence.Should().Be(0);
    }

    [Fact]
    public void Normalize_OversizeDescription_TruncatedWithMarker()
    {
        var huge = new string('x', ObservationCaps.DescriptionMaxChars + 200);
        var fields = Make(description: huge);

        var result = _normalizer.Normalize(fields, "tester", id: 1, new HashSet<string>(), NullLogger.Instance);

        result.Description.Length.Should().BeLessThanOrEqualTo(ObservationCaps.DescriptionMaxChars);
        result.Description.Should().Contain("[truncated");
    }

    [Fact]
    public void Normalize_CategoryDuplicatingConcern_DroppedToNull()
    {
        var fields = Make(category: "Security") with { Concern = ObservationConcern.Security };

        var result = _normalizer.Normalize(fields, "tester", id: 1, new HashSet<string>(), NullLogger.Instance);

        result.Category.Should().BeNull("a Category that just echoes Concern is noise");
    }

    [Fact]
    public void Normalize_DistinctCategory_PreservedAsIs()
    {
        var fields = Make(category: "secrets") with { Concern = ObservationConcern.Security };

        var result = _normalizer.Normalize(fields, "tester", id: 1, new HashSet<string>(), NullLogger.Instance);

        result.Category.Should().Be("secrets");
    }

    [Fact]
    public void Normalize_NullOptionalFields_DefaultsApplied()
    {
        var fields = Make() with { EvidenceMode = null, ReviewStatus = null };

        var result = _normalizer.Normalize(fields, "tester", id: 1, new HashSet<string>(), NullLogger.Instance);

        result.EvidenceMode.Should().Be(EvidenceMode.Potential);
        result.ReviewStatus.Should().Be("not_reviewed");
    }

    [Fact]
    public void Normalize_AssignsSuppliedRoleAndId()
    {
        var fields = Make();

        var result = _normalizer.Normalize(fields, "auditor", id: 42, new HashSet<string>(), NullLogger.Instance);

        result.Role.Should().Be("auditor");
        result.Id.Should().Be(42);
    }

    private static RawObservationFields Make(
        string description = "finding",
        int confidence = 70,
        string? category = null) => new(
        Concern: ObservationConcern.Correctness,
        Description: description,
        Suggestion: null,
        Blocking: false,
        Severity: ObservationSeverity.Medium,
        Confidence: confidence,
        Rationale: null,
        Effort: null,
        File: null,
        StartLine: 0,
        EndLine: null,
        ApiPath: null,
        SchemaName: null,
        EvidenceMode: null,
        ReviewStatus: null,
        Category: category,
        Details: null);
}
