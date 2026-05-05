using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Sandbox.Agent.Tests.Models;

public class StepSerializationTests
{
    [Fact]
    public void Serialize_RunStep_ProducesCamelCaseStringEnums()
    {
        var step = new Step(
            SchemaVersion: 1,
            StepId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Kind: StepKind.Run,
            Command: "echo",
            Args: new[] { "hello" },
            WorkingDirectory: "/work",
            Env: new Dictionary<string, string> { ["FOO"] = "bar" },
            TimeoutSeconds: 60);

        var json = JsonSerializer.Serialize(step, WireFormat.Json);

        json.Should().Contain("\"schemaVersion\":1");
        json.Should().Contain("\"kind\":\"run\"");
        json.Should().Contain("\"command\":\"echo\"");
    }

    [Fact]
    public void RoundTrip_RunStep_PreservesAllFields()
    {
        var original = new Step(1, Guid.NewGuid(), StepKind.Run, "ls", new[] { "-la" },
            "/tmp", new Dictionary<string, string> { ["A"] = "B" }, 30);

        var json = JsonSerializer.Serialize(original, WireFormat.Json);
        var roundtripped = JsonSerializer.Deserialize<Step>(json, WireFormat.Json);

        roundtripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void RoundTrip_ShutdownStep_PreservesKind()
    {
        var id = Guid.NewGuid();
        var original = Step.Shutdown(id);

        var json = JsonSerializer.Serialize(original, WireFormat.Json);
        var roundtripped = JsonSerializer.Deserialize<Step>(json, WireFormat.Json);

        roundtripped.Should().NotBeNull();
        roundtripped!.Kind.Should().Be(StepKind.Shutdown);
        roundtripped.StepId.Should().Be(id);
    }

    [Fact]
    public void Deserialize_StepWithoutKindField_DefaultsToRun()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "stepId": "22222222-2222-2222-2222-222222222222",
              "command": "echo",
              "args": ["hello"]
            }
            """;

        var step = JsonSerializer.Deserialize<Step>(json, WireFormat.Json);

        step.Should().NotBeNull();
        step!.Kind.Should().Be(StepKind.Run);
        step.TimeoutSeconds.Should().Be(Step.DefaultTimeoutSeconds);
    }

    [Fact]
    public void Deserialize_StepWithMissingTimeout_UsesDefault()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "stepId": "33333333-3333-3333-3333-333333333333",
              "kind": "run",
              "command": "true"
            }
            """;

        var step = JsonSerializer.Deserialize<Step>(json, WireFormat.Json);

        step!.TimeoutSeconds.Should().Be(Step.DefaultTimeoutSeconds);
    }

    [Fact]
    public void RoundTrip_StepEvent_PreservesAllFields()
    {
        var original = new StepEvent(1, Guid.NewGuid(), StepEventKind.Stdout, "hello",
            DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(original, WireFormat.Json);
        var roundtripped = JsonSerializer.Deserialize<StepEvent>(json, WireFormat.Json);

        roundtripped.Should().BeEquivalentTo(original,
            opts => opts.Using<DateTimeOffset>(ctx =>
                ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromMilliseconds(1))).WhenTypeIs<DateTimeOffset>());
    }

    [Fact]
    public void RoundTrip_StepResult_PreservesAllFields()
    {
        var original = new StepResult(1, Guid.NewGuid(), 0, false, 1.234, null);

        var json = JsonSerializer.Serialize(original, WireFormat.Json);
        var roundtripped = JsonSerializer.Deserialize<StepResult>(json, WireFormat.Json);

        roundtripped.Should().BeEquivalentTo(original);
    }
}
