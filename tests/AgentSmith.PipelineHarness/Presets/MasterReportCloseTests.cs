using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-22-96c8a: a spec's coding master ends on its report — the text of the last reply of a pass that ended on
/// its own — and the verify stages decide delivery. No closing tool is offered or asked for; what the report says
/// about its own outcome is shown, never acted on; a spec nobody reported on is not delivered.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class MasterReportCloseTests
{
    private const string Report = "## Delivered\n- Patch.cs answers an empty body with 400; build and tests green";
    private const string Done = """{"items":[{"id":"guard","activity":"Answer an empty request body with 400","status":"done"}]}""";

    private static RealCompositionHarness Harness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);

    private static SpecAccount Account(PipelineRunner runner) =>
        RunAccountLedger.Current(runner.LastContext!).Specs.Single().Accounts.Single();

    [Fact]
    public async Task TextOnlyReport_AllStepsDone_StagesGreen_IsDelivered()
    {
        // The 845e / 4351b shape: every step done, the report answered as text only, no closing call.
        await using var harness = Harness();
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("update_progress", Done)
            .EnqueueText(Report);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        Account(runner).Report.Should().Be(Report);
        harness.ChatClient.PromptsSeen.Should().NotContain(p => p.Contains("finish_spec"), "nothing asks for a closing tool");
        harness.ChatClient.PromptsSeen.Should().NotContain(p => p.Contains("These checklist steps are still open"),
            "a drained ledger is not re-driven");
        harness.ChatClient.LastOptions!.Tools!.Select(t => t.Name).Should().NotContain(["finish_spec", "hand_forward"]);
        harness.ChatClient.ReportReads.Should().ContainSingle().Which.Options!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>();
    }

    [Fact]
    public async Task ReportSaysBlocked_StagesGreen_IsDelivered_ReportShownAsWritten()
    {
        const string blocked = "## Blocked\n- the feed answers 401, so the package could not be updated";
        await using var harness = Harness();
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// partial"}""")
            .EnqueueToolCall("update_progress", Done)
            .EnqueueText(blocked);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue("what the report says about its own outcome is shown, never acted on: " + result.Message);
        Account(runner).Report.Should().Be(blocked);
    }

    [Fact]
    public async Task IterationCeilingOnAToolCall_LedgerDrained_StagesGreen_FailsNoReport()
    {
        await using var harness = Harness();
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("update_progress", Done)
            .EnqueueToolCall("run_command", """{"command":"dotnet test","repo":"primary"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet test","repo":"primary"}""")
            // 2026-11-22-96c8a: asked once for its report, it answers nothing.
            .EnqueueText(string.Empty);

        var runner = new PipelineRunner(harness.Services)
        {
            AgentOverride = new AgentConfig { Type = "claude", Model = "sonnet", MaxMasterLoopIterations = 2 },
        };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeFalse("the loop was cut on a call and no pass ended on a report");
        result.Message.Should().Contain("ended without writing a report");
        runner.LastContext!.Get<MasterVerification>(ContextKeys.MasterVerification).Report.Should().BeNull();
    }

    [Fact]
    public async Task OlderCatalog_CallsFinishSpecTwice_EndsOnItsReport()
    {
        // A catalog older than coding-agent-master 3.5.0 still says to call finish_spec. The tool is gone: the call is
        // answered 'not found' (no exception, so the error tolerance does not end the loop), and the report is the last
        // reply that called no tool.
        await using var harness = Harness();
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("update_progress", Done)
            .EnqueueDeferred(() => new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new TextContent(Report), new FunctionCallContent("f1", "finish_spec", new Dictionary<string, object?> { ["status"] = "green" })])))
            .EnqueueDeferred(() => new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("f2", "finish_spec", new Dictionary<string, object?> { ["status"] = "green" })])))
            .EnqueueText(Report);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        Account(runner).Report.Should().Be(Report);
        harness.ChatClient.CallMessages.SelectMany(m => m).SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Where(r => r.CallId is "f1" or "f2").DistinctBy(r => r.CallId)
            .Should().HaveCount(2).And.OnlyContain(r => r.Result!.ToString()!.Contains("not found"));
    }

    [Fact]
    public async Task ReaderFails_DeliveredByTheStages_TheReportStands()
    {
        await using var harness = Harness();
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("update_progress", Done)
            .EnqueueText(Report)
            .EnqueueReportReadFailure(new HttpRequestException("the summarization deployment is down"));

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue("a failed read never changes a delivery the stages decided: " + result.Message);
        Account(runner).Report.Should().Be(Report);
        runner.LastContext!.Get<MasterVerification>(ContextKeys.MasterVerification).ReadProblem
            .Should().Contain("the summarization deployment is down");
    }
}
