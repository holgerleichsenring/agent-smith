using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// A bound dialog turn on a worked ticket, its poster holding runs.control, run through the real master handler —
/// the composition a steward or design-partner turn gets, so its tool surface is the one a live turn is offered.
/// </summary>
internal static class BoundDialogTurn
{
    /// <summary>A turn led by <paramref name="master"/> with every bound-ticket port seeded.</summary>
    internal static AgenticMasterContext Context(string master)
    {
        var context = MasterHandlerFixture.BuildContext(master, includeTicket: false);
        var pipeline = context.Pipeline;
        pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        pipeline.Set(ContextKeys.RunId, "run-da5ef");
        pipeline.Set(ContextKeys.DialogueJobId, "s-da5ef");
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
            [new SpecDialogTurn(SpecDialogTurn.UserRole, "why did attempt 2 stop?")]);
        pipeline.Set(ContextKeys.SpecDialogWithdrawal, Mock.Of<IFiledTicketWithdrawal>());
        pipeline.Set(ContextKeys.SpecDialogTicketReader, Mock.Of<ITicketReader>());
        pipeline.Set(ContextKeys.SpecDialogTicketRuns, Mock.Of<IBoundTicketRuns>());
        pipeline.Set(ContextKeys.SpecDialogAttemptStart, Mock.Of<IAttemptStart>());
        pipeline.Set(ContextKeys.SpecDialogSeriesRead, Mock.Of<ISeriesRead>());
        pipeline.Set(ContextKeys.SpecDialogSeriesEdit, Mock.Of<ISeriesEdit>());
        return context;
    }

    internal static Task Run(
        IAgenticLoopRunner loop, AgenticMasterContext context, MasterHandlerFixture.StubSubAgentRunner? children = null) =>
        MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(context.MasterSkillName, "body"), null,
                0, subAgents: children ?? new MasterHandlerFixture.StubSubAgentRunner(), limits: new LoopLimitsConfig())
            .ExecuteAsync(context, CancellationToken.None);

    /// <summary>The tool names the master of a bound turn led by <paramref name="master"/> is offered.</summary>
    internal static async Task<IReadOnlyList<string>> MasterSurface(string master)
    {
        var loop = new SurfaceCapturingLoop();
        await Run(loop, Context(master));
        return loop.MasterTools;
    }
}
