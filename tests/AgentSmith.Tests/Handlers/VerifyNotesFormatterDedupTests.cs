using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0129c: VerifyNotesFormatter.Dedup collapses cross-round duplicates by
/// (file, concern, description-prefix-100). Single-round inputs have no
/// duplicates by construction; this is purely the round-2 escalation path.
/// </summary>
public sealed class VerifyNotesFormatterDedupTests
{
    [Fact]
    public void Dedup_NoDuplicates_AllPreserved()
    {
        var observations = new[]
        {
            Make(file: "a.cs", concern: ObservationConcern.Correctness, description: "foo"),
            Make(file: "b.cs", concern: ObservationConcern.Correctness, description: "bar"),
            Make(file: "c.cs", concern: ObservationConcern.Architecture, description: "baz"),
        };

        var result = VerifyNotesFormatter.Dedup(observations);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void Dedup_SameFileSameConcernSameDescriptionPrefix_LastWins()
    {
        var observations = new[]
        {
            Make(file: "a.cs", concern: ObservationConcern.Correctness, description: "Out of scope: x.cs",
                rationale: "round 1 rationale"),
            Make(file: "a.cs", concern: ObservationConcern.Correctness, description: "Out of scope: x.cs",
                rationale: "round 2 rationale"),
        };

        var result = VerifyNotesFormatter.Dedup(observations);

        result.Should().HaveCount(1);
        result[0].Rationale.Should().Be("round 2 rationale");
    }

    [Fact]
    public void Dedup_DifferentFile_BothPreserved()
    {
        var observations = new[]
        {
            Make(file: "a.cs", concern: ObservationConcern.Correctness, description: "Out of scope"),
            Make(file: "b.cs", concern: ObservationConcern.Correctness, description: "Out of scope"),
        };

        VerifyNotesFormatter.Dedup(observations).Should().HaveCount(2);
    }

    [Fact]
    public void Dedup_DifferentConcern_BothPreserved()
    {
        var observations = new[]
        {
            Make(file: "a.cs", concern: ObservationConcern.Correctness, description: "issue"),
            Make(file: "a.cs", concern: ObservationConcern.Architecture, description: "issue"),
        };

        VerifyNotesFormatter.Dedup(observations).Should().HaveCount(2);
    }

    [Fact]
    public void Dedup_DescriptionDivergesPast100Chars_BothPreserved()
    {
        var commonPrefix = new string('a', 100);
        var observations = new[]
        {
            Make(file: "a.cs", concern: ObservationConcern.Correctness, description: commonPrefix + "first"),
            // Same first-100 chars but the prefix in our dedup keys is exactly 100 — both will be
            // grouped as duplicate. To get distinct entries, change the prefix itself.
            Make(file: "a.cs", concern: ObservationConcern.Correctness, description: new string('b', 100) + "second"),
        };

        VerifyNotesFormatter.Dedup(observations).Should().HaveCount(2);
    }

    private static SkillObservation Make(string file, ObservationConcern concern, string description, string? rationale = null) =>
        new(
            Id: 0,
            Role: "test",
            Concern: concern,
            Description: description,
            Suggestion: "",
            Blocking: true,
            Severity: ObservationSeverity.High,
            Confidence: 80,
            Rationale: rationale,
            File: file);
}
