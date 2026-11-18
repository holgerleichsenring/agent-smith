using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-11-18-98fa: a spec's coding master ends with its markdown report and a finish_spec call; the handler publishes
/// what finish_spec recorded with the report beside it, and parses no text. Without the call nothing is published and
/// the spec's account names why.
/// </summary>
public sealed class FinishSpecHandlerTests
{
    private const string Report = "## Delivered\n- the checkout, built and tested\n\n## Open\n- CSV export";

    [Fact]
    public async Task AMasterThatReportsAndCallsFinishSpec_PublishesTheVerificationWithTheReport()
    {
        var context = SpecContext();
        var loop = new FinishingLoop(finishOnDrive: 0);

        await Run(loop, context);

        var v = context.Pipeline.Get<MasterVerification>(ContextKeys.MasterVerification);
        (v.Status, v.Report).Should().Be((VerificationStatus.Green, Report));
        v.FollowUps.Should().Equal(new MasterFollowUp("CSV export over paging", "its own design"));
        loop.Prompts.Should().NotContain(p => p.Contains("did not call finish_spec"), "a master that finished is not asked again");
        MasterReportAccount.Missing(context.Pipeline).Should().BeNull();
    }

    [Fact]
    public async Task AMasterThatEndsOnAVerdictBlockWithoutFinishSpec_PublishesNothing_AndTheSpecSaysWhy()
    {
        var context = SpecContext();
        var loop = new FinishingLoop(finishOnDrive: null,
            text: "Done.\n```verdict\n{ \"status\": \"green\", \"summary\": \"ok\" }\n```");

        await Run(loop, context);

        context.Pipeline.Has(ContextKeys.MasterVerification).Should().BeFalse("no text is parsed");
        loop.Prompts.Should().Contain(p => p.Contains("did not call finish_spec"), "it was asked for it once");
        MasterReportAccount.Missing(context.Pipeline).Should().Contain("ended without finish_spec");
    }

    [Fact]
    public async Task AReportOnOneDrive_AndFinishSpecOnTheNudge_KeepsTheReport()
    {
        var context = SpecContext();
        var loop = new FinishingLoop(finishOnDrive: 1, reportOnlyOnFirst: true);

        await Run(loop, context);

        context.Pipeline.Get<MasterVerification>(ContextKeys.MasterVerification).Report.Should().Be(Report);
    }

    private static AgenticMasterContext SpecContext()
    {
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        context.Pipeline.Set(ContextKeys.PipelineName, "code");
        context.Pipeline.Set(ContextKeys.CurrentSpec,
            new SpecDraft("2026-11-18-0000a", "build the checkout", "spec: 2026-11-18-0000a\ngoal: build the checkout\n", [])
            {
                Done = ["the checkout is built"],
            });
        return context;
    }

    private static Task Run(FinishingLoop loop, AgenticMasterContext context) =>
        MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body"))
            .ExecuteAsync(context, CancellationToken.None);

    /// <summary>Answers each drive; on the given one it writes the report and calls finish_spec as the tool layer
    /// would.</summary>
    private sealed class FinishingLoop(int? finishOnDrive, string text = Report, bool reportOnlyOnFirst = false) : IAgenticLoopRunner
    {
        public int Drives { get; private set; }
        public List<string> Prompts { get; } = [];

        public async Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            var drive = Drives++;
            Prompts.Add(request.UserPrompt);
            var said = reportOnlyOnFirst && drive > 0 ? string.Empty : text;
            List<AIContent> contents = said.Length > 0 ? [new TextContent(said)] : [];
            if (drive == finishOnDrive)
            {
                var finish = request.Tools!.OfType<AIFunction>().Single(t => t.Name == FinishSpecToolHost.Name);
                var args = new Dictionary<string, object?>
                {
                    ["status"] = "green", ["summary"] = "the checkout",
                    ["follow_ups"] = System.Text.Json.JsonSerializer.SerializeToElement(new[] { new { goal = "CSV export over paging", why = "its own design" } }),
                };
                await finish.InvokeAsync(new AIFunctionArguments(args), cancellationToken);
                contents.Add(new FunctionCallContent("f", FinishSpecToolHost.Name, args));
            }
            return new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            }, TimeSpan.FromSeconds(1));
        }
    }
}
