using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-fbbda through the REAL composition: a house '### Limits' fence sets function_lines 20
/// with tolerance_percent 25, so the band ends at 25. A 24-line method passes the commit gate and
/// is named in the pull request as a divergence; a 30-line method is past the band and gets the
/// one correction pass, which is asked to reach the limit.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class StructureToleranceHarnessTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    private const string Principles =
        "## Limits\n\n```yaml\nfunction_lines: 55\n```\n\n## Project Specifics\n\n"
        + "### Limits\n\n```yaml\nfunction_lines: 20\ntolerance_percent: 25\n```\n";

    // A method of n statements measures n + 3 lines: signature, two braces.
    private static string Handler(params (string Name, int Statements)[] methods) =>
        "class Handler\n{\n" + string.Concat(methods.Select(m => Method(m.Name, m.Statements))) + "}\n";

    private static string Method(string name, int count) =>
        $"    void {name}()\n    {{\n"
        + string.Concat(Enumerable.Range(0, count).Select(i => $"        Step{i}();\n")) + "    }\n";

    private static string Write(string content) =>
        $$"""{"path":"primary/src/Handler.cs","content":{{System.Text.Json.JsonSerializer.Serialize(content)}}}""";

    private static RealCompositionHarness Harness()
    {
        var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.StubSandboxFactory!.BaseFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".agentsmith/contexts/default/principles.md"] = Principles,
            [".agentsmith/contexts/default/structure-baseline.tsv"] = string.Empty,
        };
        return harness;
    }

    [Fact]
    public async Task Harness_MethodInsideTheBand_PassesAndIsNamedInThePullRequest()
    {
        await using var harness = Harness();
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will add the handler.")
            .EnqueueToolCall("write_file", Write(Handler(("Handle", 21))))
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        harness.ChatClient.ToolCalls.Count(c => c.Name == "write_file").Should().Be(1, "a divergence gets no correction");
        runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Specs.Should().ContainSingle(p => p.State == SpecRunState.Done);
        var bodies = runner.LastContext!.Get<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies);
        bodies.Values.Should().Contain(b =>
            b.Contains("- within tolerance: src/Handler.cs Handler.Handle() function_lines 24/20", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Harness_MethodPastTheBand_GetsTheCorrectionPass()
    {
        await using var harness = Harness();
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will add the handler.")
            .EnqueueToolCall("write_file", Write(Handler(("Handle", 27))))
            .EnqueueText(GreenVerdict)
            .EnqueueText("Correcting: I split Handle by responsibility.")
            .EnqueueToolCall("write_file", Write(Handler(("Handle", 2), ("First", 14), ("Second", 13))))
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        harness.ChatClient.ToolCalls.Count(c => c.Name == "write_file").Should().Be(2, "one correction pass");
        harness.ChatClient.PromptsSeen.Should().Contain(p =>
            p.Contains("src/Handler.cs Handler.Handle(): function_lines 30 > 20", StringComparison.Ordinal)
            && p.Contains("the limit itself, not the tolerance band", StringComparison.Ordinal));
        runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Specs.Should().ContainSingle(p => p.State == SpecRunState.Done);
        var bodies = runner.LastContext!.Get<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies);
        bodies.Values.Should().NotContain(b => b.Contains("within tolerance", StringComparison.Ordinal),
            "the split methods are all within their limit");
    }
}
