using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-28-db88d through the real composition: a spec run's log_decision lands in decisions/{specId}.yaml of the spec
/// the step works — the step's spec, not a file named after the run — and a second run of that spec appends to the file
/// the first one left on the branch.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DecisionFilePerSpecTests
{
    private static readonly HarnessSeries.Member Guard = new("Introduce the empty-payload guard", "An empty request body is answered with 400.");
    private static readonly string SpecId = HarnessSeries.Base + "a";
    private static readonly string SpecFile = $".agentsmith/decisions/{SpecId}.yaml";

    [Fact]
    public async Task Harness_TwoRunsOfOneSpec_AppendToOneDecisionFile()
    {
        var first = await RunTheSpecAsync("**Guard first**: one check at the entry point", seeded: null);
        first.Should().StartWith($"spec: {SpecId}\ndecisions:\n").And.Contain("Guard first");

        var second = await RunTheSpecAsync("**Callers keep their names**: no rename in this spec", seeded: first);
        second.Should().StartWith($"spec: {SpecId}\ndecisions:\n")
            .And.Contain("Guard first", "the second run appends to what the first one left")
            .And.Contain("Callers keep their names");
    }

    // One run of the spec whose master logs one decision; what it wrote to the spec's decision file. seeded is the file as
    // an earlier run committed it to the branch the run is cut from.
    private static async Task<string> RunTheSpecAsync(string decision, string? seeded)
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        if (seeded is not null) harness.StubSandboxFactory!.SourceFiles = new() { [SpecFile] = seeded };
        harness.ChatClient
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("log_decision", System.Text.Json.JsonSerializer.Serialize(new { category = "Implementation", decision }))
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueReport();

        await new PipelineRunner(harness.Services).WithSeries(Guard).RunAsync("code");

        var writes = harness.StubSandboxFactory!.Spawned.SelectMany(s => s.Sandbox.RanSteps)
            .Where(s => s.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile && s.Path != null
                && s.Path.Contains(".agentsmith/decisions/", StringComparison.Ordinal))
            .ToList();
        writes.Should().NotContain(s => s.Path!.Contains("harness-", StringComparison.Ordinal),
            "no decision file is named after the run");
        return writes.Last(s => s.Path!.EndsWith(SpecFile, StringComparison.Ordinal)).Content!;
    }
}
