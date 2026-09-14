using AgentSmith.Contracts.Commands;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-08-805f: a master whose checklist is complete owes its verdict. Two live runs
/// on 0.145.0 marked every ledger item done and then kept calling tools — sixteen minutes
/// of re-verifying on one, twenty-five calls and a question to the operator on the other —
/// because the idle stop fires only on a turn WITHOUT a tool call. The brake below
/// UseFunctionInvocation demands the verdict once after the allowance and ends the pass
/// after the allowance again; this suite drives it through the real composition.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class VerdictOwedTests
{
    // agent.verdict_owed_after_iterations, the fixture config states none.
    private const int Allowance = 3;
    private const string ReadPath = "primary/src/Patch.cs";

    [Fact]
    public async Task FixBug_LedgerCompleteAndTheMasterKeepsReading_TheBrakeEndsThePass()
    {
        // The master writes, builds, marks its whole ledger done — and then answers every
        // further call with a read. On main nothing ends that but the function-invoking
        // client's iteration ceiling; the brake ends it after the demand and the allowance.
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        ScriptCompletedLedger(harness);
        for (var i = 0; i < 250; i++)
            harness.ChatClient.EnqueueToolCall("read_file", $$$"""{"path":"{{{ReadPath}}}"}""");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-bug");

        ReadsOfThePatch(harness).Should().Be(
            2 * Allowance,
            "after the checklist is complete the master gets the allowance, ONE demand for the "
            + "verdict, the allowance again, and then the pass ends");
        runner.LastContext!.Has(ContextKeys.MasterVerification).Should().BeFalse(
            "the pass the brake ended carries no verdict — the master's verdict is recorded as unknown; "
            + "whether the run delivered is the account's judgement of the branch (p0421), which this "
            + "brake does not touch");
        result.Should().NotBeNull("the run reaches a terminal result instead of the iteration ceiling");
    }

    [Fact]
    public async Task FixBug_LedgerCompleteAndTheDemandIsAnswered_RunGreen()
    {
        // The allowance is spent on reads; the call after it carries the demand as a user turn
        // appended behind the last tool result, and the master answers it with the verdict.
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        ScriptCompletedLedger(harness);
        for (var i = 0; i < Allowance; i++)
            harness.ChatClient.EnqueueToolCall("read_file", $$$"""{"path":"{{{ReadPath}}}"}""");
        harness.ChatClient.EnqueueText(
            """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-bug");

        harness.ChatClient.CallMessages.Should().Contain(
            call => call.Count > 2
                && call[call.Count - 1].Role == ChatRole.User
                && call[call.Count - 2].Role == ChatRole.Tool,
            "the demand is a user turn injected behind the last tool result of the running pass");
        ReadsOfThePatch(harness).Should().Be(Allowance, "the demand was answered, not read past");
        result.IsSuccess.Should().BeTrue(
            $"a real change and a green verdict answered to the demand pass the keystone: {result.Message}");
    }

    private static void ScriptCompletedLedger(RealCompositionHarness harness) =>
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueToolCall("update_progress",
                """{"items":[{"id":"guard","activity":"Answer an empty request body with 400","status":"done"}]}""");

    // The read steps that reached the sandbox for the scripted path — what the master
    // actually ran, as opposed to what the script offered.
    private static int ReadsOfThePatch(RealCompositionHarness harness) =>
        harness.StubSandboxFactory!.Spawned
            .SelectMany(s => s.Sandbox.RanSteps)
            .Count(s => s.Kind == StepKind.ReadFile
                && s.Path is { } p && p.EndsWith("src/Patch.cs", StringComparison.Ordinal));
}
