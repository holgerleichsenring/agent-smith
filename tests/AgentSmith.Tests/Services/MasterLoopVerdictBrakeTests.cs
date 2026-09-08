using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

// 2026-09-08-805f: the ledger-complete brake under a REAL FunctionInvokingChatClient, the way
// ChatClientFactory chains it. A fake provider marks the ledger done on its first turn and then
// reads forever (or answers the demand with a verdict); the brake decides how the pass ends.
public sealed class MasterLoopVerdictBrakeTests
{
    private const string Demand = "[demand] your checklist is complete — emit the verdict";
    private const string Verdict = """{"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true}""";

    // Turn 0 calls update_progress (completing the ledger); every later turn calls read_file,
    // unless the previous request carried the demand and answerDemand is set — then it is the
    // verdict, text only.
    private sealed class ReadingInner(bool answerDemand) : IChatClient
    {
        public List<List<ChatMessage>> Forwarded { get; } = new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var list = messages.ToList();
            Forwarded.Add(list);
            var turn = Forwarded.Count - 1;
            var demanded = list.Any(m => m.Role == ChatRole.User && (m.Text ?? "").Contains("[demand]"));
            ChatMessage reply = turn == 0
                ? new ChatMessage(ChatRole.Assistant, [Call("c0", "update_progress")])
                : demanded && answerDemand
                    ? new ChatMessage(ChatRole.Assistant, Verdict)
                    : new ChatMessage(ChatRole.Assistant, [Call($"c{turn}", "read_file")]);
            return Task.FromResult(new ChatResponse(reply));
        }

        private static FunctionCallContent Call(string id, string name) =>
            new(id, name, new Dictionary<string, object?>());

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class Rig
    {
        public bool LedgerComplete;
        public int Reads;
        public int? StoppedAfter;
        public List<AITool> Tools { get; }

        public Rig()
        {
            Tools =
            [
                AIFunctionFactory.Create(() => { LedgerComplete = true; return "ledger: done"; }, name: "update_progress"),
                AIFunctionFactory.Create(() => { Reads++; return "content"; }, name: "read_file"),
            ];
        }

        public MasterLoopHooks Hooks(int allowance, bool withLedgerHook = true) => new(
            IsLedgerComplete: withLedgerHook ? () => LedgerComplete : null,
            RenderVerdictDemand: () => Demand,
            OnVerdictBrake: turns => StoppedAfter = turns,
            VerdictOwedAfterIterations: allowance,
            ReminderEveryNIterations: 0, DriftEditlessIterations: 0);
    }

    private static IChatClient Loop(IChatClient inner, MasterLoopHooks hooks) =>
        new ChatClientBuilder(new MasterLoopGovernorChatClient(inner, hooks))
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = 200)
            .Build();

    private static Task<ChatResponse> Run(IChatClient loop, Rig rig) =>
        loop.GetResponseAsync([new ChatMessage(ChatRole.User, "go")], new ChatOptions { Tools = rig.Tools });

    [Fact]
    public async Task VerdictBrake_LedgerCompleteThenNToolTurns_TheDemandIsInjected()
    {
        var rig = new Rig();
        var inner = new ReadingInner(answerDemand: true);

        await Run(Loop(inner, rig.Hooks(allowance: 3)), rig);

        // Request 0 completes the ledger; requests 1..3 are the allowance; request 4 carries the demand.
        inner.Forwarded.Take(4).Should().OnlyContain(req => !req.Any(m => (m.Text ?? "").Contains("[demand]")));
        inner.Forwarded[4].Last().Role.Should().Be(ChatRole.User, "the demand is appended behind the last tool result");
        inner.Forwarded[4].Last().Text.Should().Be(Demand);
    }

    [Fact]
    public async Task VerdictBrake_TheDemandIsAnswered_TheLoopEndsOnTheModelsTurn()
    {
        var rig = new Rig();
        var inner = new ReadingInner(answerDemand: true);

        var response = await Run(Loop(inner, rig.Hooks(allowance: 3)), rig);

        rig.Reads.Should().Be(3, "the allowance was spent on reads, the demand on the verdict");
        response.Text.Should().Contain("\"status\":\"green\"", "the pass ends on the model's own verdict turn");
        rig.StoppedAfter.Should().BeNull("the brake never had to end the pass");
    }

    [Fact]
    public async Task VerdictBrake_ToolsContinueAfterTheDemand_ThePassEndsWithoutAVerdict()
    {
        var rig = new Rig();
        var inner = new ReadingInner(answerDemand: false);

        var response = await Run(Loop(inner, rig.Hooks(allowance: 3)), rig);

        rig.Reads.Should().Be(6, "the allowance before the demand and the allowance after it");
        inner.Forwarded.Should().HaveCount(7, "no provider call is made for the synthetic end of the pass");
        rig.StoppedAfter.Should().Be(6);
        response.Text.Should().Contain(LedgerCompleteBrake.EndOfPassText);
        response.Messages.Last().Contents.OfType<FunctionCallContent>().Should().BeEmpty(
            "the pass ends on a tool-less turn, so the function-invoking client returns");
    }

    [Fact]
    public async Task VerdictBrake_NoLedgerHook_NeverFires()
    {
        var rig = new Rig();
        var inner = new ReadingInner(answerDemand: false);

        await Run(Loop(inner, rig.Hooks(allowance: 3, withLedgerHook: false)), rig);

        rig.Reads.Should().Be(199, "only the iteration ceiling ends a pass without the brake");
        rig.StoppedAfter.Should().BeNull();
    }
}
