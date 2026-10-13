using AgentSmith.Application.Services.Expectations;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-13-3890a: a master that stops on its own word — declined criteria, a blocker, a red
/// verdict on a drained ledger — while a ratified criterion is not Met and budget remains is driven
/// once more with its own reasons; the loop end is recorded on the trail.
/// </summary>
public sealed class MasterSurrenderChallengeTests
{
    private const string Criterion = "an HTTP test proves an expired link returns Expired";

    private static string Verdict(string status, string disposition, string evidence) => $$"""
        ```verdict
        { "status": "{{status}}", "build_ran": true, "build_passed": true, "tests_ran": true,
          "tests_passed": {{(status == "green" ? "true" : "false")}}, "summary": "s",
          "acceptance": [ { "criterion": "{{Criterion}}", "status": "{{disposition}}", "evidence": "{{evidence}}" } ] }
        ```
        """;

    private static readonly string Declined = Verdict("green", "not_applicable", "Required regression test was not added");

    [Fact]
    public async Task Challenge_DeclinedCriteriaWithBudget_DrivesOnePass()
    {
        var loop = new ScriptedPasses(Declined, Declined, Declined);
        var decisions = new CapturedDecisions();

        await RunAsync(loop, decisions);

        var challenges = loop.Seen.Where(r => r.UserPrompt.Contains("you declined it")).ToList();
        challenges.Should().ContainSingle("a declined stop is challenged exactly once");
        challenges[0].UserPrompt.Should().Contain("Required regression test was not added")
            .And.Contain(MasterChallengeNudge.WorkNotReasons);
        decisions.Lines.Should().ContainSingle(l => l.Contains("Master loop ended: Declined") && l.Contains("after one challenge pass"));
    }

    [Fact]
    public async Task Challenge_RespectedBlocker_DrivesOnePass()
    {
        var blocked = Verdict("failed", "unmet", "migration not generated")
            + "\n```json\n{ \"blocked\": true, \"blocker\": \"dotnet-ef is unavailable on PATH\" }\n```";
        var loop = new ScriptedPasses(Verdict("green", "unmet", "not started"), blocked, blocked);

        await RunAsync(loop, new CapturedDecisions());

        loop.Seen.Where(r => r.UserPrompt.Contains("Your stated blocker: dotnet-ef is unavailable on PATH"))
            .Should().ContainSingle("the blocker is put back in front of the master once, even on a red drained ledger");
    }

    [Fact]
    public async Task Challenge_RestatedStop_EndsTheLoop()
    {
        var loop = new ScriptedPasses(Declined, Declined, Declined, Declined, Declined);
        var decisions = new CapturedDecisions();

        await RunAsync(loop, decisions);

        loop.Seen.Count(r => r.UserPrompt.Contains("you declined it")).Should().Be(1);
        decisions.Lines.Should().ContainSingle(l => l.Contains("Master loop ended"));
    }

    [Fact]
    public void Challenge_BudgetExhaustedOrIdle_NotChallenged()
    {
        var verdict = Parsed(Declined);

        new MasterSurrenderChallenge().TryChallenge(MasterStopKind.Declined, budgetExhausted: true, [Criterion], verdict, null)
            .Should().BeFalse("money decides first");
        new MasterSurrenderChallenge().TryChallenge(MasterStopKind.Idle, budgetExhausted: false, [Criterion], verdict, null)
            .Should().BeFalse("an idle pass is a stall, not a surrender");
    }

    [Fact]
    public void Challenge_ToolLessPassWithDecliningVerdict_IsChallenged()
    {
        var challenge = new MasterSurrenderChallenge();

        var stop = challenge.ClassifyBottom(ReengageOutcome.StopIdle, Parsed(Declined), new ProgressLedger([]), [Criterion]);

        stop.Should().Be(MasterStopKind.Declined, "a pass that called no tool is read by its verdict first");
        challenge.TryChallenge(stop, budgetExhausted: false, [Criterion], Parsed(Declined), null).Should().BeTrue();
    }

    [Fact]
    public void Challenge_NoRatifiedCriteria_NotChallenged() =>
        new MasterSurrenderChallenge().TryChallenge(MasterStopKind.RedDrained, budgetExhausted: false, [], Parsed(Declined), null)
            .Should().BeFalse();

    [Fact]
    public async Task MasterLoop_End_RecordsTheStop()
    {
        var met = Verdict("green", "met", "AccessLinkHttpTests.cs:50");
        var decisions = new CapturedDecisions();

        await RunAsync(new ScriptedPasses(met), decisions);

        decisions.Lines.Should().ContainSingle(l => l.Contains("Master loop ended: Satisfied") && !l.Contains("challenge"));
    }

    [Fact]
    public void DeclineRule_NamesInstallableToolsAsWork()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft("p1", "g", "spec: p1", []) { Done = [Criterion] });

        ExpectationPromptSection.Build(pipeline).Should().Contain("install it with run_command");
    }

    private static MasterVerification Parsed(string text) =>
        AgentSmith.Application.Services.MasterVerificationParser.TryParse(text)!;

    private static async Task RunAsync(ScriptedPasses loop, IDecisionLogger decisions)
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        context.Pipeline.Set(ContextKeys.PipelineName, "code");
        context.Pipeline.Set(ContextKeys.CurrentSpec,
            new SpecDraft("p1", "expired links", "spec: p1", []) { Done = [Criterion] });
        await MasterHandlerFixture.Build(loop, prompts, decisions: decisions).ExecuteAsync(context, CancellationToken.None);
    }

    private sealed class CapturedDecisions : IDecisionLogger
    {
        public List<string> Lines { get; } = [];

        public Task<DecisionLogOutcome> LogAsync(ISandboxFileReader? repositoryFiles, DecisionCategory category,
            string decision, CancellationToken cancellationToken = default, string? sourceLabel = null)
        {
            Lines.Add(decision);
            return Task.FromResult(DecisionLogOutcome.Recorded);
        }
    }

    /// <summary>Answers with scripted text and one tool call per pass, so the driver sees work done.</summary>
    private sealed class ScriptedPasses(params string[] texts) : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = [];
        private int _call;

        internal IReadOnlyList<AgenticLoopRequest> Seen => _seen;

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            _seen.Add(request);
            var text = texts[Math.Min(_call, texts.Length - 1)];
            _call++;
            var call = new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent($"c{_call}", "grep_in_tree", new Dictionary<string, object?>())]);
            var result = new ChatMessage(ChatRole.Tool, [new FunctionResultContent($"c{_call}", "no matches")]);
            var response = new ChatResponse([call, result, new ChatMessage(ChatRole.Assistant, text)])
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }
}
