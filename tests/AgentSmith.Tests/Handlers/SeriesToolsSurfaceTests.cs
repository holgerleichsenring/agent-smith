using Microsoft.Extensions.AI;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-14-da5ee: read_series and verify_series join the design master of a bound turn seeded with the series
/// read — a worked ticket — and edit_series only beside the edit port; no child surface carries any of them.
/// </summary>
public sealed class SeriesToolsSurfaceTests
{
    private const string DesignMaster = "design-partner-master";
    private const string Steward = PipelinePresets.TicketStewardMaster;

    // 2026-11-18-9c1cd: read_series and verify_series are read_ticket's part series (verify: true); edit_series is
    // offered on every steward turn and answers why when the poster may not.
    [Fact]
    public async Task StewardTurn_SeededReadAndEdit_ReadsAndEditsTheSeries()
    {
        var loop = new SurfaceCapturingLoop();

        await Run(loop, Steward, DesignContext(Steward, edit: true));

        loop.MasterTools.Should().Contain(["read_ticket", "edit_series"]).And.NotContain(["read_series", "verify_series"]);
        loop.Tools.Takes("read_ticket", "verify").Should().BeTrue();
    }

    [Fact]
    public async Task StewardTurn_ReadWithoutEdit_EditSeriesSaysWhy()
    {
        var loop = new SurfaceCapturingLoop();

        await Run(loop, Steward, DesignContext(Steward, edit: false));

        var answer = await Invoke(loop, "edit_series", new AIFunctionArguments { ["edits"] = Array.Empty<SeriesEdit>() });
        answer.Should().Contain("runs.control");
    }

    [Fact]
    public async Task DesignTurn_NeverWorkedTicket_SeriesPartSaysSo_NoEditSeries()
    {
        var loop = new SurfaceCapturingLoop();

        await Run(loop, DesignMaster, DesignContext(DesignMaster, edit: false, read: false));

        loop.MasterTools.Should().NotContain("edit_series");
        (await Invoke(loop, "read_ticket", new AIFunctionArguments { ["part"] = "series" })).Should().Contain("no series yet");
    }

    [Fact]
    public async Task SpecDialogSubAgent_ChildTools_DoNotIncludeSeriesTools()
    {
        var loop = new SurfaceCapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();

        await Run(loop, Steward, DesignContext(Steward, edit: true), children);

        var child = children.SeenContexts.Single().ChildTools;
        child.Select(t => t.Name).Should().NotContain(["edit_series", "start_next_attempt"], "a child acts on no person's word");
        var series = await child.OfType<AIFunction>().Single(t => t.Name == "read_ticket")
            .InvokeAsync(new AIFunctionArguments { ["part"] = "series" });
        series!.ToString().Should().Contain("no series", "the series is the master's to read and act on");
    }

    private static async Task<string> Invoke(SurfaceCapturingLoop loop, string tool, AIFunctionArguments args) =>
        (await loop.Tools.OfType<AIFunction>().Single(t => t.Name == tool).InvokeAsync(args))!.ToString()!;

    private static Task Run(
        SurfaceCapturingLoop loop, string master, AgenticMasterContext context,
        MasterHandlerFixture.StubSubAgentRunner? children = null) =>
        MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(master, "body"), null,
                0, subAgents: children ?? new MasterHandlerFixture.StubSubAgentRunner(), limits: new LoopLimitsConfig())
            .ExecuteAsync(context, CancellationToken.None);

    private static AgenticMasterContext DesignContext(string master, bool edit, bool read = true)
    {
        var context = MasterHandlerFixture.BuildContext(master, includeTicket: false);
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set(ContextKeys.DialogueJobId, "s-series");
        context.Pipeline.Set(ContextKeys.SpecDialogTicketReader, Mock.Of<ITicketReader>());
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
        [
            new SpecDialogTurn(SpecDialogTurn.UserRole, "drop the second spec"),
        ]);
        if (read) context.Pipeline.Set(ContextKeys.SpecDialogSeriesRead, Mock.Of<ISeriesRead>());
        if (edit) context.Pipeline.Set(ContextKeys.SpecDialogSeriesEdit, Mock.Of<ISeriesEdit>());
        return context;
    }
}
