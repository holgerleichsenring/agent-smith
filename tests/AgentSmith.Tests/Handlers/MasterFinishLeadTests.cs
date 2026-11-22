using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-11-22-6fd7a: run 845e — every checklist step done, build and tests green, the report answered as text and no
/// finish_spec, twice, while the re-engagement nudge said steps were still open. A master whose checklist is done is
/// told exactly what is missing: the report and the finish_spec call in the same reply.
/// </summary>
public sealed class MasterFinishLeadTests
{
    private const string Report = "## Delivered\n- 25 files, build and 41 tests green";

    private static ProgressLedger Ledger(params ProgressStatus[] statuses) =>
        new([.. statuses.Select((s, i) => new ProgressLedgerEntry($"{i + 1}", $"step {i + 1}", s))]);

    private static readonly MasterVerification Green = new(VerificationStatus.Green, true, true, true, true, "ok");

    private static async Task<AgenticMasterContext> Run(ReportAsTextLoop loop)
    {
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        context.Pipeline.Set(ContextKeys.PipelineName, "code");
        context.Pipeline.Set(ContextKeys.CurrentSpec,
            new SpecDraft("2026-11-22-0000a", "build it", "spec: 2026-11-22-0000a\ngoal: build it\n", []) { Done = ["it is built"] });
        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body"))
            .ExecuteAsync(context, CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task AllStepsDone_ReportAsText_TheVerdictNudgeSaysWhatIsMissing_AndTheSpecIsReported()
    {
        var loop = new ReportAsTextLoop(answerLeadFrom: 0);
        var context = await Run(loop);

        var lead = loop.Prompts.FindIndex(p => p.StartsWith(MasterFinishLead.Text, StringComparison.Ordinal));
        lead.Should().BePositive("the verdict nudge over a done checklist opens with the lead");
        loop.Prompts[lead].Should().Contain("one reply whose text is your report").And.NotContain("Build the project and run the tests");
        loop.Prompts.Should().HaveCount(lead + 1, "finish_spec was recorded, so nothing drives again");
        var v = context.Pipeline.Get<MasterVerification>(ContextKeys.MasterVerification);
        (v.Status, v.Report).Should().Be((VerificationStatus.Green, Report));
        MasterReportAccount.Missing(context.Pipeline).Should().BeNull("the spec is reported, so it can be delivered");
    }

    [Fact]
    public async Task AMasterThatAnswersTheVerdictNudgeAsTextAgain_IsToldTheSameOnReengagement()
    {
        var loop = new ReportAsTextLoop(answerLeadFrom: 1);
        var context = await Run(loop);

        loop.Prompts.Count(p => p.StartsWith(MasterFinishLead.Text, StringComparison.Ordinal))
            .Should().Be(2, "the verdict nudge and the re-engagement both say what is missing");
        loop.Prompts.Should().NotContain(p => p.Contains("These checklist steps are still open"));
        context.Pipeline.Get<MasterVerification>(ContextKeys.MasterVerification).Report.Should().Be(Report);
    }

    [Fact]
    public void ReengageNudge_AllStepsDoneNoVerdict_NamesFinishSpecInTheSameReply_AndSaysNoStepIsOpen()
    {
        MasterNudges.BuildReengageNudge("task", Ledger(ProgressStatus.Done, ProgressStatus.Done), [], null)
            .Should().StartWith(MasterFinishLead.Text).And.Contain("one reply whose text is your report")
            .And.NotContain("still open");
    }

    [Fact]
    public void ReengageNudge_OpenSteps_KeepsTheOpenStepsText()
    {
        MasterNudges.BuildReengageNudge("task", Ledger(ProgressStatus.Done, ProgressStatus.Pending), [], null)
            .Should().Contain("These checklist steps are still open").And.NotContain(MasterFinishLead.Text);
    }

    [Fact]
    public void VerdictNudge_OpenSteps_AsksForTheCheck_AndTheCallInTheSameReply()
    {
        MasterNudges.BuildVerdictNudge("task", Ledger(ProgressStatus.Pending))
            .Should().Contain("Build the project and run the tests").And.Contain("one reply whose text is your report");
    }

    [Fact]
    public void ApplyNudge_ClosesWithOneReplyThatCarriesTheReportAndTheCall()
    {
        MasterNudges.BuildApplyNudge("task", ProgressLedger.Empty)
            .Should().Contain("one reply whose text is your report").And.NotContain("write your report and call finish_spec");
    }

    [Fact]
    public void FinishSpecTool_SaysItIsCalledInTheReplyThatCarriesTheReport_OnBothSurfaces()
    {
        var host = new FinishSpecToolHost();
        var descriptions = new[]
        {
            host.GetTools(null, null).Single().Description,
            MergedTools.FinishSpec(host, null).Description,
        };
        descriptions.Should().AllSatisfy(d => d.Should().StartWith("Ends this spec for the pipeline. Call it in the same reply as your report")
            .And.NotContain("after your report").And.NotContain("just before this call"));
        host.FinishSpec("green", "built").Should().EndWith("Your report is the text of the reply that made this call, or your last text before it when that reply had none.");
    }

    [Fact]
    public void Due_OnlyOverADoneChecklistWithoutAVerdict()
    {
        MasterFinishLead.Due(Ledger(ProgressStatus.Done), null).Should().BeTrue();
        MasterFinishLead.Due(Ledger(ProgressStatus.Done, ProgressStatus.InProgress), null).Should().BeFalse("work is open");
        MasterFinishLead.Due(ProgressLedger.Empty, null).Should().BeFalse("no checklist says the work is done");
        MasterFinishLead.Due(Ledger(ProgressStatus.Done), Green).Should().BeFalse("finish_spec was called");
    }

    /// <summary>Pass 0 marks every step done and answers its report as text; later drives answer the report as text
    /// again, until the n-th drive that opens with the lead, which it answers with the report and finish_spec.</summary>
    private sealed class ReportAsTextLoop(int answerLeadFrom) : IAgenticLoopRunner
    {
        private int _leads;
        public List<string> Prompts { get; } = [];

        public async Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken ct)
        {
            Prompts.Add(request.UserPrompt);
            List<AIContent> contents = [new TextContent(Report)];
            if (Prompts.Count == 1)
            {
                var steps = JsonSerializer.SerializeToElement(new[] { new { id = "1", activity = "build", status = "done" } });
                await Tool(request, "update_progress").InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["items"] = steps }), ct);
            }
            else if (request.UserPrompt.StartsWith(MasterFinishLead.Text, StringComparison.Ordinal) && _leads++ >= answerLeadFrom)
            {
                var args = new Dictionary<string, object?> { ["status"] = "green", ["summary"] = "built" };
                await Tool(request, FinishSpecToolHost.Name).InvokeAsync(new AIFunctionArguments(args), ct);
                contents.Add(new FunctionCallContent("f", FinishSpecToolHost.Name, args));
            }
            return new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            }, TimeSpan.FromSeconds(1));
        }

        private static AIFunction Tool(AgenticLoopRequest request, string name) =>
            request.Tools.OfType<AIFunction>().Single(t => t.Name == name);
    }
}
