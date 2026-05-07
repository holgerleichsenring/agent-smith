using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class GateOutputHandlerTests
{
    private readonly GateOutputHandler _handler = new(NullLogger<GateOutputHandler>.Instance);

    private static RoleSkillDefinition CreateRole(string name = "test-gate") => new()
    {
        Name = name,
        DisplayName = "Test Gate",
        Emoji = "🔒"
    };

    private static SkillOrchestration CreateOrchestration(params string[] inputCategories) => new(
        OrchestrationRole.Gate,
        SkillOutputType.List,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        inputCategories);

    private static string BuildGateJson(params (string description, string category)[] items)
    {
        var entries = items.Select(i =>
            $$"""{"description":"{{i.description}}","file":"test.cs","start_line":1,"severity":"high","category":"{{i.category}}"}""");
        return $$"""{"confirmed":[{{string.Join(",", entries)}}],"rejected":[]}""";
    }

    [Fact]
    public void SingleGate_ReplacesOwnCategory()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<List<SkillObservation>>(ContextKeys.SkillObservations, new()
        {
            ObservationFactory.Make("HIGH", "a.cs", 1, "Secret leak", "desc", 90, category: "secrets"),
            ObservationFactory.Make("MEDIUM", "b.cs", 2, "Old dep", "desc", 80, category: "dependencies"),
        });

        var orch = CreateOrchestration("secrets");
        var json = BuildGateJson(("Confirmed secret", "secrets"));

        var result = _handler.Handle(CreateRole(), orch, json, pipeline);

        result.IsSuccess.Should().BeTrue();

        var observations = pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var o)
            ? o! : [];

        observations.Should().HaveCount(2);
        observations.Should().Contain(x => x.Description.Contains("Confirmed secret") && x.Category == "secrets");
        observations.Should().Contain(x => x.Description.Contains("Old dep") && x.Category == "dependencies");
    }

    [Fact]
    public void TwoGates_MergesResults()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<List<SkillObservation>>(ContextKeys.SkillObservations, new()
        {
            ObservationFactory.Make("HIGH", "a.cs", 1, "Secret leak", "desc", 90, category: "secrets"),
            ObservationFactory.Make("MEDIUM", "b.cs", 2, "SQL injection", "desc", 80, category: "injection"),
            ObservationFactory.Make("LOW", "c.cs", 3, "Old dep", "desc", 70, category: "dependencies"),
        });

        var orch1 = CreateOrchestration("secrets");
        var json1 = BuildGateJson(("Confirmed secret", "secrets"));
        _handler.Handle(CreateRole("secrets-gate"), orch1, json1, pipeline);

        var orch2 = CreateOrchestration("injection");
        var json2 = BuildGateJson(("Confirmed injection", "injection"));
        _handler.Handle(CreateRole("injection-gate"), orch2, json2, pipeline);

        var observations = pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var o)
            ? o! : [];

        observations.Should().HaveCount(3);
        observations.Should().Contain(x => x.Description.Contains("Confirmed secret") && x.Category == "secrets");
        observations.Should().Contain(x => x.Description.Contains("Confirmed injection") && x.Category == "injection");
        observations.Should().Contain(x => x.Description.Contains("Old dep") && x.Category == "dependencies");
    }

    [Fact]
    public void UncategorizedObservations_PassThrough()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<List<SkillObservation>>(ContextKeys.SkillObservations, new()
        {
            ObservationFactory.Make("HIGH", "a.cs", 1, "Secret", "desc", 90, category: "secrets"),
            ObservationFactory.Make("LOW", "d.cs", 4, "Misc finding", "desc", 50, category: "unknown"),
        });

        var orch = CreateOrchestration("secrets");
        var json = """{"confirmed":[],"rejected":[]}""";

        _handler.Handle(CreateRole(), orch, json, pipeline);

        var observations = pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var o)
            ? o! : [];

        observations.Should().HaveCount(1);
        observations.Should().Contain(x => x.Description.Contains("Misc finding") && x.Category == "unknown");
    }

    [Fact]
    public void WildcardCategories_ReplacesAllObservations()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<List<SkillObservation>>(ContextKeys.SkillObservations, new()
        {
            ObservationFactory.Make("HIGH", "a.cs", 1, "Old finding", "desc", 90, category: "secrets"),
            ObservationFactory.Make("MEDIUM", "b.cs", 2, "Another old", "desc", 80, category: "injection"),
        });

        var orch = CreateOrchestration("*");
        var json = BuildGateJson(("New finding", "secrets"));

        _handler.Handle(CreateRole(), orch, json, pipeline);

        var observations = pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var o)
            ? o! : [];

        observations.Should().HaveCount(1);
        observations.Single().Description.Should().Contain("New finding");
    }

    [Fact]
    public void InvalidJson_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        var orch = CreateOrchestration("secrets");

        var result = _handler.Handle(CreateRole(), orch, "not json at all {", pipeline);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("invalid JSON");
    }

    [Fact]
    public void MissingConfirmedProperty_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        var orch = CreateOrchestration("secrets");

        var result = _handler.Handle(CreateRole(), orch, """{"findings":[]}""", pipeline);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("missing 'confirmed'");
    }

    [Fact]
    public void EmptyResponse_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        var orch = CreateOrchestration("secrets");

        var result = _handler.Handle(CreateRole(), orch, "   ", pipeline);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("empty LLM response");
    }

    [Fact]
    public void Verdict_MissingPassProperty_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        var orch = new SkillOrchestration(
            OrchestrationRole.Gate, SkillOutputType.Verdict,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>());

        var result = _handler.Handle(CreateRole(), orch, """{"reason":"ok"}""", pipeline);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("missing 'pass'");
    }

    [Fact]
    public void Verdict_InvalidJson_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        var orch = new SkillOrchestration(
            OrchestrationRole.Gate, SkillOutputType.Verdict,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>());

        var result = _handler.Handle(CreateRole(), orch, "garbage", pipeline);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("invalid JSON");
    }
}
