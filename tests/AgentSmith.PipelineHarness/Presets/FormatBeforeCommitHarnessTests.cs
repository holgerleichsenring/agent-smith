using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-fbbdb: through the REAL composition, a context that declares its formatter sees it
/// run over the file the master changed, at the repository root, right before the spec commit's
/// checkpoint push.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class FormatBeforeCommitHarnessTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    private const string ContextYaml = """
        meta:
          workdir: .
        stack:
          lang: csharp
        format:
          command: dotnet csharpier {files}
        """;

    [Fact]
    public async Task Harness_ContextDeclaresFormat_RunsItOverChangedPathsBeforeCheckpointPush()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        ((StubSourceProviderFactory)harness.Services.GetRequiredService<ISourceProviderFactory>()).ContextYaml = ContextYaml;
        harness.StubSandboxFactory!.BaseFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        harness.StubSandboxFactory.ReportsWorkingTree = true;
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will add the handler.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Handler.cs","content":"class Handler {}\n"}""")
            .EnqueueText(GreenVerdict);

        await new PipelineRunner(harness.Services).RunAsync("code");

        var steps = harness.StubSandboxFactory.Spawned.SelectMany(s => s.Sandbox.RanSteps).ToList();
        var format = steps.FindIndex(step => step.Command == "/bin/sh" && step.Args is [_, var line]
            && line.StartsWith("dotnet csharpier", StringComparison.Ordinal));
        format.Should().BeGreaterThanOrEqualTo(0, "the declared formatter ran");
        steps[format].Args![1].Should().Be("dotnet csharpier './src/Handler.cs'",
            "the formatter gets the file the spec changed, and the run record stays out of it");
        steps[format].WorkingDirectory.Should().Be("/work");
        IsCheckpointStart(steps[format + 1]).Should().BeTrue(
            "the checkpoint push is the next thing the spec commit does");
    }

    private static bool IsCheckpointStart(Step step) =>
        step.Command == "git" && step.Args is ["status", "--porcelain"];
}
