using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
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
        SkillRole.Gate,
        SkillOutputType.List,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        inputCategories);

    private static string BuildGateJson(params (string title, string category)[] findings)
    {
        var items = findings.Select(f =>
            $$"""{"title":"{{f.title}}","file":"test.cs","line":1,"severity":"HIGH","reason":"r","category":"{{f.category}}"}""");
        return $$"""{"confirmed":[{{string.Join(",", items)}}],"rejected":[]}""";
    }

    [Fact]
    public void SingleGate_ReplacesOwnCategory()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ExtractedFindings,
            (IReadOnlyList<Finding>)new List<Finding>
            {
                new("HIGH", "a.cs", 1, null, "Secret leak", "desc", 9, Category: "secrets"),
                new("MEDIUM", "b.cs", 2, null, "Old dep", "desc", 8, Category: "dependencies"),
            }.AsReadOnly());

        var orch = CreateOrchestration("secrets");
        var json = BuildGateJson(("Confirmed secret", "secrets"));

        var result = _handler.Handle(CreateRole(), orch, json, pipeline);

        result.IsSuccess.Should().BeTrue();

        var findings = pipeline.TryGet<IReadOnlyList<Finding>>(ContextKeys.ExtractedFindings, out var f)
            ? f! : [];

        findings.Should().HaveCount(2);
        findings.Should().Contain(x => x.Title == "Confirmed secret" && x.Category == "secrets");
        findings.Should().Contain(x => x.Title == "Old dep" && x.Category == "dependencies");
    }

    [Fact]
    public void TwoGates_MergesResults()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ExtractedFindings,
            (IReadOnlyList<Finding>)new List<Finding>
            {
                new("HIGH", "a.cs", 1, null, "Secret leak", "desc", 9, Category: "secrets"),
                new("MEDIUM", "b.cs", 2, null, "SQL injection", "desc", 8, Category: "injection"),
                new("LOW", "c.cs", 3, null, "Old dep", "desc", 7, Category: "dependencies"),
            }.AsReadOnly());

        // First gate: secrets
        var orch1 = CreateOrchestration("secrets");
        var json1 = BuildGateJson(("Confirmed secret", "secrets"));
        _handler.Handle(CreateRole("secrets-gate"), orch1, json1, pipeline);

        // Second gate: injection
        var orch2 = CreateOrchestration("injection");
        var json2 = BuildGateJson(("Confirmed injection", "injection"));
        _handler.Handle(CreateRole("injection-gate"), orch2, json2, pipeline);

        var findings = pipeline.TryGet<IReadOnlyList<Finding>>(ContextKeys.ExtractedFindings, out var f)
            ? f! : [];

        findings.Should().HaveCount(3);
        findings.Should().Contain(x => x.Title == "Confirmed secret" && x.Category == "secrets");
        findings.Should().Contain(x => x.Title == "Confirmed injection" && x.Category == "injection");
        findings.Should().Contain(x => x.Title == "Old dep" && x.Category == "dependencies");
    }

    [Fact]
    public void UncategorizedFindings_PassThrough()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ExtractedFindings,
            (IReadOnlyList<Finding>)new List<Finding>
            {
                new("HIGH", "a.cs", 1, null, "Secret", "desc", 9, Category: "secrets"),
                new("LOW", "d.cs", 4, null, "Misc finding", "desc", 5, Category: "unknown"),
            }.AsReadOnly());

        var orch = CreateOrchestration("secrets");
        // Gate confirms nothing — all secrets filtered out
        var json = """{"confirmed":[],"rejected":[]}""";

        _handler.Handle(CreateRole(), orch, json, pipeline);

        var findings = pipeline.TryGet<IReadOnlyList<Finding>>(ContextKeys.ExtractedFindings, out var f)
            ? f! : [];

        findings.Should().HaveCount(1);
        findings.Should().Contain(x => x.Title == "Misc finding" && x.Category == "unknown");
    }

    [Fact]
    public void Finding_HasCategory_FromSource()
    {
        var finding = new Finding("HIGH", "a.cs", 1, null, "Test", "desc", 9, Category: "secrets");
        finding.Category.Should().Be("secrets");

        var defaultFinding = new Finding("HIGH", "a.cs", 1, null, "Test", "desc", 9);
        defaultFinding.Category.Should().Be("unknown");
    }

    [Fact]
    public void WildcardCategories_ReplacesAllFindings()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ExtractedFindings,
            (IReadOnlyList<Finding>)new List<Finding>
            {
                new("HIGH", "a.cs", 1, null, "Old finding", "desc", 9, Category: "secrets"),
                new("MEDIUM", "b.cs", 2, null, "Another old", "desc", 8, Category: "injection"),
            }.AsReadOnly());

        var orch = CreateOrchestration("*");
        var json = BuildGateJson(("New finding", "secrets"));

        _handler.Handle(CreateRole(), orch, json, pipeline);

        var findings = pipeline.TryGet<IReadOnlyList<Finding>>(ContextKeys.ExtractedFindings, out var f)
            ? f! : [];

        findings.Should().HaveCount(1);
        findings.Single().Title.Should().Be("New finding");
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
            SkillRole.Gate, SkillOutputType.Verdict,
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
            SkillRole.Gate, SkillOutputType.Verdict,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>());

        var result = _handler.Handle(CreateRole(), orch, "garbage", pipeline);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("invalid JSON");
    }
}
