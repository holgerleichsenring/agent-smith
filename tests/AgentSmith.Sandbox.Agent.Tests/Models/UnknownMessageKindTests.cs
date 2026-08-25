using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Sandbox.Agent.Tests.Models;

/// <summary>
/// 2026-08-25-0d01: the wire's enum converter used to throw on a value it did not know.
/// That throw came out of the agent's blocking read, escaped a loop catching only Redis
/// faults, exited the container, and reached the operator as "sandbox vanished" — a newer
/// server sending an older agent one message kind it had never heard of was indistinguishable
/// from a dead pod. Tolerating the value is what makes any protocol report possible at all.
/// </summary>
public sealed class UnknownMessageKindTests
{
    [Fact]
    public void Deserialize_AStepKindThisBuildDoesNotKnow_DoesNotThrow()
    {
        var json = $$"""
            {"schemaVersion":{{WireProtocol.Current}},"stepId":"{{Guid.NewGuid()}}","kind":"summonDragon"}
            """;

        var act = () => JsonSerializer.Deserialize<Step>(json, WireFormat.Json);

        act.Should().NotThrow<JsonException>();
    }

    [Fact]
    public void Deserialize_AStepKindThisBuildDoesNotKnow_IsCarriedAsUnknown()
    {
        var json = $$"""
            {"schemaVersion":{{WireProtocol.Current}},"stepId":"{{Guid.NewGuid()}}","kind":"summonDragon"}
            """;

        JsonSerializer.Deserialize<Step>(json, WireFormat.Json)!.Kind.Should().Be(StepKind.Unknown);
    }

    [Fact]
    public void Validate_AnUnknownStepKind_AnswersWithTheProtocolItSpeaks()
    {
        var step = new Step(WireProtocol.Current, Guid.NewGuid(), StepKind.Unknown);

        var (isValid, error) = step.Validate();

        isValid.Should().BeFalse("nothing can be executed for a kind this build cannot name");
        error.Should().Contain(WireProtocol.Window,
            "the answer travels back on the result channel, so it has to say what this "
            + "build speaks rather than only that something went wrong");
    }

    [Fact]
    public void Deserialize_AnUnknownEventKind_IsCarriedAsUnknown()
    {
        var json = $$"""
            {"schemaVersion":{{WireProtocol.Current}},"stepId":"{{Guid.NewGuid()}}",
             "kind":"telepathy","line":"hi","timestamp":"2026-08-25T00:00:00+00:00"}
            """;

        JsonSerializer.Deserialize<StepEvent>(json, WireFormat.Json)!.Kind
            .Should().Be(StepEventKind.Unknown);
    }

    // A formatting preference has a safe substitute, so it falls back to its own default
    // rather than to an Unknown nobody could act on.
    [Fact]
    public void Deserialize_AnUnknownOutputMode_FallsBackToTheDefault()
    {
        var json = $$"""
            {"schemaVersion":{{WireProtocol.Current}},"stepId":"{{Guid.NewGuid()}}",
             "kind":"grep","path":"/x","pattern":"y","outputMode":"interpretiveDance"}
            """;

        var step = JsonSerializer.Deserialize<Step>(json, WireFormat.Json)!;

        step.OutputMode.Should().Be(GrepOutputMode.Content);
        step.Kind.Should().Be(StepKind.Grep, "the rest of the message is still perfectly readable");
    }

    [Fact]
    public void Serialize_AKnownKind_KeepsTheCamelCaseNameItAlwaysHad()
    {
        var step = new Step(WireProtocol.Current, Guid.NewGuid(), StepKind.DirectoryTree, Path: "/x");

        JsonSerializer.Serialize(step, WireFormat.Json)
            .Should().Contain("\"directoryTree\"", "the tolerant converter replaces the stock "
                + "one, so it has to write exactly what the stock one wrote");
    }
}
