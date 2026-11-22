using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-11-22-96c8a: a spec's coding master ends on its report — the text of the final reply of a pass that ended on
/// its own. The handler publishes it with what one read takes off it; no closing tool is offered or asked for. A pass
/// cut on a call leaves the report standing; without any report the spec's account names why.
/// </summary>
public sealed class MasterReportHandlerTests
{
    private const string Report = "## Delivered\n- the checkout, built and tested\n\n## Follow-ups\n- CSV export over paging";
    private const string Read = """{"follow_ups":[{"goal":"CSV export over paging","why":"its own design"}]}""";

    [Fact]
    public async Task AMasterThatEndsOnItsReport_PublishesTheReport_WithTheReadsFollowUps()
    {
        var context = SpecContext();
        var loop = new ReportLoop(Ended(Report));

        await Run(loop, context, new ScriptedChatClientFactory(() => Answer(Read)));

        var v = context.Pipeline.Get<MasterVerification>(ContextKeys.MasterVerification);
        (v.Report, v.ReadProblem).Should().Be((Report, (string?)null));
        v.FollowUps.Should().Equal(new MasterFollowUp("CSV export over paging", "its own design"));
        loop.Prompts.Should().NotContain(p => p.Contains("finish_spec"), "no prompt asks for a closing tool");
        loop.Tools.Should().NotContain(["finish_spec", "hand_forward"]);
        MasterReportAccount.Missing(context.Pipeline).Should().BeNull();
    }

    [Fact]
    public async Task AMasterWhosePassesAreCutOnACall_PublishesNoReport_AndTheSpecSaysWhy()
    {
        var context = SpecContext();
        var loop = new ReportLoop(CutOnACall, CutOnACall);

        await Run(loop, context, new ScriptedChatClientFactory(() => Answer(Read)));

        context.Pipeline.Get<MasterVerification>(ContextKeys.MasterVerification).Report.Should().BeNull();
        loop.Prompts.Count(p => p == AgentSmith.Application.Services.Handlers.MasterNudges.AskForReport)
            .Should().Be(1, "asked once, then the run decides as always");
        MasterReportAccount.Missing(context.Pipeline).Should().Be(MasterReportAccount.NoReport);
    }

    [Fact]
    public async Task AMasterEndingOnNoText_IsAskedOnceForItsReport_AndItsAnswerIsTheReport()
    {
        var context = SpecContext();
        var loop = new ReportLoop(CutOnACall, CutOnACall, Ended(Report));

        await Run(loop, context, new ScriptedChatClientFactory(() => Answer("{}")));

        loop.Prompts.Count(p => p == AgentSmith.Application.Services.Handlers.MasterNudges.AskForReport).Should().Be(1);
        context.Pipeline.Get<MasterVerification>(ContextKeys.MasterVerification).Report.Should().Be(Report);
    }

    [Fact]
    public async Task AReportOnOneDrive_ThenADriveCutOnACall_KeepsTheReport()
    {
        var context = SpecContext();
        var loop = new ReportLoop(Ended(Report), CutOnACall);

        await Run(loop, context, new ScriptedChatClientFactory(() => Answer("{}")));

        loop.Drives.Should().Be(2, "the apply drive ran after the report");
        context.Pipeline.Get<MasterVerification>(ContextKeys.MasterVerification).Report.Should().Be(Report);
    }

    [Fact]
    public async Task ALaterDriveThatEndsOnText_IsAddedToTheReport()
    {
        var context = SpecContext();
        var loop = new ReportLoop(Ended(Report), Ended("## Delivered\n- all of it again"));

        await Run(loop, context, new ScriptedChatClientFactory(() => Answer("{}")));

        context.Pipeline.Get<MasterVerification>(ContextKeys.MasterVerification).Report
            .Should().Be(Report + "\n\n## Delivered\n- all of it again", "pass 0's report is never lost");
    }

    /// <summary>2026-11-22-96c8a: a stage repair of the same spec adds its one-line report after the original one, which
    /// stays first; an identical text is kept once.</summary>
    [Fact]
    public async Task AStageRepairsOneLineReport_KeepsTheOriginalReportFirst()
    {
        var context = SpecContext();
        context.Pipeline.Set(ContextKeys.MasterVerification, new MasterVerification(Report));
        var loop = new ReportLoop(Ended("Fixed the missing semicolon."), Ended(Report));

        await Run(loop, context, new ScriptedChatClientFactory(() => Answer("{}")));

        context.Pipeline.Get<MasterVerification>(ContextKeys.MasterVerification).Report
            .Should().Be(Report + "\n\nFixed the missing semicolon.", "the original stays first and is not repeated");
    }

    [Fact]
    public async Task ReadFails_TheReportStands_AndTheProblemIsNamed()
    {
        var context = SpecContext();
        var loop = new ReportLoop(Ended(Report));

        await Run(loop, context, new ScriptedChatClientFactory(() => throw new HttpRequestException("summarization is down")));

        var v = context.Pipeline.Get<MasterVerification>(ContextKeys.MasterVerification);
        v.Report.Should().Be(Report);
        v.FollowUps.Should().BeNull();
        v.ReadProblem.Should().Contain("summarization is down");
        MasterReportAccount.Missing(context.Pipeline).Should().BeNull("a failed read never takes the report away");
    }

    private static ChatResponse Answer(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    private static AgenticMasterContext SpecContext()
    {
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        context.Pipeline.Set(ContextKeys.PipelineName, "code");
        context.Pipeline.Set(ContextKeys.CurrentSpec,
            new SpecDraft("2026-11-22-0000a", "build the checkout", "spec: 2026-11-22-0000a\ngoal: build the checkout\n", [])
            {
                Done = ["the checkout is built"],
            });
        return context;
    }

    private static Task Run(ReportLoop loop, AgenticMasterContext context, ScriptedChatClientFactory reads) =>
        MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body"), reportReads: reads)
            .ExecuteAsync(context, CancellationToken.None);

    private static ChatResponse Ended(string text) => new([
        new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c1", "read_file")]),
        new ChatMessage(ChatRole.Assistant, text)]);

    private static ChatResponse CutOnACall => new([new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c2", "read_file")])]);

    /// <summary>Answers each drive with the next scripted response; the last repeats.</summary>
    private sealed class ReportLoop(params ChatResponse[] drives) : IAgenticLoopRunner
    {
        public int Drives { get; private set; }
        public List<string> Prompts { get; } = [];
        public List<string> Tools { get; } = [];

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            Prompts.Add(request.UserPrompt);
            Tools.AddRange(request.Tools?.Select(t => t.Name) ?? []);
            var response = drives[Math.Min(Drives++, drives.Length - 1)];
            response.Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }
}
