using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Tests.Handlers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog.Thread;

/// <summary>A design turn's reply cut at the role's output limit is re-asked once under the dialog budget.</summary>
public sealed class SpecDialogOutcomeGateLengthTests
{
    private const string Draft = "Here is the spec:\n```yaml\nspec: x\ngoal: \"half a";

    [Fact]
    public async Task SpecDialogOutcomeGate_LengthFinish_RePromptsWithTheRaisedBudget()
    {
        var loop = new QueuedLoop(Cut("the answer, cut again"));

        var result = await GateAsync(loop, Cut("the answer"), MayPropose());

        loop.Requests.Should().ContainSingle("a cut is re-asked once").Which.MaxOutputTokensOverride.Should().Be(32000);
        result.Response.Text.Should().Contain("cut off at the output limit", "a second cut fails honestly");
    }

    [Fact]
    public async Task SpecDialogOutcomeGate_CutDraft_LengthCheckedBeforeTheRefusal()
    {
        var loop = new QueuedLoop(new ChatResponse(new ChatMessage(ChatRole.Assistant, "The whole answer.")));

        var result = await GateAsync(loop, Cut(Draft), FirstTurn());

        var retry = loop.Requests.Should().ContainSingle().Subject;
        retry.MaxOutputTokensOverride.Should().Be(32000);
        retry.UserPrompt.Should().Contain("cut off at the output limit").And.NotContain("proposed work before");
        result.Response.Text.Should().Be("The whole answer.");
    }

    private static async Task<AgenticLoopResult> GateAsync(QueuedLoop loop, ChatResponse first, IReadOnlyList<SpecDialogTurn> transcript)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogTranscript, transcript);
        var request = new AgenticLoopRequest(new AgentConfig(), TaskType.Primary, "system", "the turn", []);
        return await MasterHandlerFixture.OutcomeGate(loop).GateAsync(pipeline, request, "the turn",
            new AgenticLoopResult(first, TimeSpan.Zero), new MasterConversation(), PipelineCostTracker.GetOrCreate(pipeline),
            CancellationToken.None);
    }

    private static ChatResponse Cut(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text)) { FinishReason = ChatFinishReason.Length };

    private static IReadOnlyList<SpecDialogTurn> FirstTurn() => [new(SpecDialogTurn.UserRole, "need a widget")];

    private static IReadOnlyList<SpecDialogTurn> MayPropose() =>
    [
        new(SpecDialogTurn.UserRole, "need a widget"),
        new(SpecDialogTurn.AssistantRole, "which ledger?", SpecDialogTurnKind.Answer),
        new(SpecDialogTurn.UserRole, "the main one"),
    ];

    private sealed class QueuedLoop(params ChatResponse[] responses) : IAgenticLoopRunner
    {
        private readonly Queue<ChatResponse> _responses = new(responses);

        public List<AgenticLoopRequest> Requests { get; } = [];

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new AgenticLoopResult(_responses.Dequeue(), TimeSpan.Zero));
        }
    }
}
