using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Turns;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-17-042ee: the master hands the design turn a tool surface that reports, and every
/// other master the same surface it always had. The wrap is on the ONE line the spec-dialog
/// surface is returned from; AgenticToolSurface itself is untouched.
/// </summary>
public sealed class MasterSurfaceActivityTests
{
    [Fact]
    public async Task Activity_SpecDialogSurface_ReportsEveryToolItOffers()
    {
        var loop = new RecordingLoopRunner("An answer with no outcome block.");

        await RunAsync(loop, SpecDialog: true);

        loop.SeenRequests[0].Tools.OfType<AIFunction>().Should().NotBeEmpty()
            .And.OnlyContain(tool => tool is ActivityReportingAIFunction);
    }

    [Fact]
    public async Task Activity_CodingSurface_IsNotWrapped()
    {
        var loop = new RecordingLoopRunner("ok");

        await RunAsync(loop, SpecDialog: false);

        loop.SeenRequests[0].Tools.OfType<AIFunction>().Should().NotBeEmpty()
            .And.NotContain(tool => tool is ActivityReportingAIFunction);
    }

    [Fact]
    public async Task DialogTurn_InvalidOutcome_ReportsRevising()
    {
        var accessor = TurnActivityRecorder.Silent();
        var recorder = new TurnActivityRecorder();
        using var observing = accessor.Observe(recorder);
        // Neither pass resolves to a terminal outcome, so the gate re-prompts once.
        var loop = new RecordingLoopRunner("```outcome\nkind: nonsense\n```");

        await RunAsync(loop, SpecDialog: true);

        loop.SeenRequests.Should().HaveCount(2, "the gate re-prompts once on an invalid outcome");
        recorder.Lines.Should().Contain("revising");
    }

    private static async Task RunAsync(RecordingLoopRunner loop, bool SpecDialog)
    {
        var master = SpecDialog ? "design-partner-master" : "coding-agent-master";
        var prompts = new MasterHandlerFixture.StubPromptCatalog(master, "body");
        var context = MasterHandlerFixture.BuildContext(master);
        if (SpecDialog)
        {
            context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
            // Discussed: an answered turn followed by an operator reply, so 2026-09-17-042ec's
            // refusal does not fire and the only re-prompt is the gate's own.
            context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(
                ContextKeys.SpecDialogTranscript,
                [
                    new SpecDialogTurn(SpecDialogTurn.UserRole, "we need widgets"),
                    new SpecDialogTurn(
                        SpecDialogTurn.AssistantRole, "Found the service; two open questions.",
                        SpecDialogTurnKind.Answer),
                    new SpecDialogTurn(SpecDialogTurn.UserRole, "cut the ordering feature into slices"),
                ]);
        }
        await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(context, CancellationToken.None);
    }

    private sealed class RecordingLoopRunner(string text) : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = [];

        public IReadOnlyList<AgenticLoopRequest> SeenRequests => _seen;

        public Task<AgenticLoopResult> RunAsync(
            AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            _seen.Add(request);
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }
}
