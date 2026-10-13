using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// The commit gate through the REAL composition: a master that writes a 70-line method under a
/// 55-line limit at the base gets one correction pass; a correction that does not split it
/// fails the spec naming the method, and one that splits it lets the spec through.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class StructureGateHarnessTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    // Signature, braces and 67 statements: a 70-line method.
    private static string LongMethod() =>
        "class Handler\n{\n" + Method("Handle", 0, 67) + "}\n";

    private static string SplitMethod() =>
        "class Handler\n{\n    void Handle()\n    {\n        First();\n        Second();\n    }\n"
        + Method("First", 0, 34) + Method("Second", 34, 33) + "}\n";

    private static string Method(string name, int from, int count) =>
        $"    void {name}()\n    {{\n"
        + string.Concat(Enumerable.Range(from, count).Select(i => $"        Step{i}();\n")) + "    }\n";

    private static string Write(string content) =>
        $$"""{"path":"primary/src/Handler.cs","content":{{System.Text.Json.JsonSerializer.Serialize(content)}}}""";

    private static RealCompositionHarness Harness()
    {
        var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.StubSandboxFactory!.BaseFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".agentsmith/contexts/default/principles.md"] = "## Limits\n\n```yaml\nfunction_lines: 55\n```\n",
            [".agentsmith/contexts/default/structure-baseline.tsv"] = string.Empty,
        };
        return harness;
    }

    [Fact]
    public async Task Harness_MasterWritesOversizedMethodTwice_OneCorrectionThenFailsNamingIt()
    {
        await using var harness = Harness();
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will add the handler.")
            .EnqueueToolCall("write_file", Write(LongMethod()))
            .EnqueueText(GreenVerdict)
            .EnqueueText("Correcting: I keep it as it is.")
            .EnqueueToolCall("write_file", Write(LongMethod()))
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeFalse(result.Message);
        result.Message.Should().Contain("src/Handler.cs Handler.Handle(): function_lines 70 > 55");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "write_file");
        harness.ChatClient.ToolCalls.Count(c => c.Name == "write_file").Should().Be(2, "one correction, no carousel");
        runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Specs.Should().ContainSingle(p => p.State == SpecRunState.Failed);
    }

    [Fact]
    public async Task Harness_CorrectionSplitsTheMethod_SpecIsDone()
    {
        await using var harness = Harness();
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will add the handler.")
            .EnqueueToolCall("write_file", Write(LongMethod()))
            .EnqueueText(GreenVerdict)
            .EnqueueText("Correcting: I split Handle by responsibility.")
            .EnqueueToolCall("write_file", Write(SplitMethod()))
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        harness.ChatClient.ToolCalls.Count(c => c.Name == "write_file").Should().Be(2);
        runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Specs.Should().ContainSingle(p => p.State == SpecRunState.Done);
        runner.LastContext!.TryGet<List<string>>(ContextKeys.StructureFindings, out _).Should().BeFalse();
    }
}
