using AgentSmith.Application.Services.WorkSpecs;
using AgentSmith.Contracts.WorkSpecs;
using FluentAssertions;

namespace AgentSmith.Tests.WorkSpecs;

// p0390: emit + consume share one YamlDotNet configuration, so a spec this system
// writes parses back by construction. These pin the round trip and the schema header
// a reviewer's editor resolves when they edit spec.yaml inside the PR.
public sealed class WorkSpecSerializerTests
{
    private readonly WorkSpecSerializer _serializer = new();

    private static WorkSpec Sample() => new(
        Key: "azuredevops-19106",
        Goal: "Migrate the exchange onto the new transport",
        Requirements: ["The producer publishes on the new transport", "No message is lost"],
        Constraints: [new WorkSpecConstraint("Queue names stay byte-for-byte as given", "queue-names")],
        Done: ["The integration suite is green against the new transport"],
        DoneIsReadOnly: true,
        Assumptions: ["The legacy transport stays available during rollout"],
        Revisions: [new WorkSpecRevision(1, "initial derivation", DateTimeOffset.UnixEpoch)]);

    [Fact]
    public void Serialize_Spec_StartsWithTheSchemaHeaderSoAReviewerGetsValidation() =>
        _serializer.Serialize(Sample()).Should().StartWith(WorkSpecSerializer.SchemaHeader);

    [Fact]
    public void Parse_SerializedSpec_RoundTripsEveryField()
    {
        var parsed = _serializer.Parse(_serializer.Serialize(Sample()));

        parsed.Should().BeEquivalentTo(Sample());
    }

    [Fact]
    public void Parse_SpecWithHandback_RestoresTheCaseCode()
    {
        var handedBack = Sample() with
        {
            Handback = new WorkSpecHandback(
                WorkSpecHandbackCase.NotImplementable, "the named API does not exist"),
        };

        var parsed = _serializer.Parse(_serializer.Serialize(handedBack));

        parsed!.Handback!.Case.Should().Be(WorkSpecHandbackCase.NotImplementable);
        parsed.IsHandedBack.Should().BeTrue();
    }

    [Fact]
    public void Parse_TextWithoutAGoal_ReturnsNull() =>
        _serializer.Parse("some: unrelated\nyaml: document").Should().BeNull();

    [Fact]
    public void Parse_Garbage_ReturnsNullInsteadOfThrowing() =>
        _serializer.Parse("goal: [unclosed").Should().BeNull();

    [Fact]
    public void Parse_SpecWithoutRevisionHeader_RecoversWithASyntheticFirstRevision()
    {
        var parsed = _serializer.Parse("goal: do the thing\nrequirements:\n  - it works\n");

        parsed!.Current.Number.Should().Be(1);
        parsed.Current.Cause.Should().Contain("recovered");
    }

    [Fact]
    public void WithRevision_ExistingSpec_AppendsAndMakesItCurrent()
    {
        var amended = Sample().WithRevision(
            new WorkSpecRevision(2, "reviewer edit in PR #7", DateTimeOffset.UnixEpoch));

        amended.Revisions.Should().HaveCount(2);
        amended.Current.Cause.Should().Be("reviewer edit in PR #7");
    }
}
