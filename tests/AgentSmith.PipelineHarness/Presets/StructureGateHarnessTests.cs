using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-06-8565d through the REAL composition: a master that writes a method longer than the
/// base's Limits gets one correction pass, and a spec whose correction does not split it fails
/// naming the method.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class StructureGateHarnessTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    private static string LongMethod() =>
        "class Handler\n{\n    void Handle()\n    {\n"
        + string.Concat(Enumerable.Range(0, 66).Select(i => $"        Step{i}();\n")) + "    }\n}\n";

    [Fact]
    public async Task Harness_MasterWritesOversizedMethodTwice_OneCorrectionThenFailsNamingIt()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.StubSandboxFactory!.BaseFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".agentsmith/contexts/default/principles.md"] = "## Limits\n\n```yaml\nfunction_lines: 55\n```\n",
            [".agentsmith/contexts/default/structure-baseline.tsv"] = string.Empty,
        };
        var write = $$"""{"path":"primary/src/Handler.cs","content":{{System.Text.Json.JsonSerializer.Serialize(LongMethod())}}}""";
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will add the handler.")
            .EnqueueToolCall("write_file", write)
            .EnqueueText(GreenVerdict)
            .EnqueueText("Correcting: I keep it as it is.")
            .EnqueueToolCall("write_file", write)
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeFalse(result.Message);
        result.Message.Should().Contain("Handler.Handle()");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "write_file");
        runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Specs.Should().ContainSingle(p => p.State == SpecRunState.Failed);
    }
}
