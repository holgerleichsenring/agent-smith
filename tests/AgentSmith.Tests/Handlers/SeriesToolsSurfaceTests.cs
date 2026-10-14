using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-14-da5ee: read_series and verify_series join the design master of a bound turn seeded with the series
/// read — a worked ticket — and edit_series only beside the edit port; no child surface carries any of them.
/// </summary>
public sealed class SeriesToolsSurfaceTests
{
    private const string DesignMaster = "design-partner-master";
    private static readonly string[] Tools = ["read_series", "verify_series", "edit_series"];

    [Fact]
    public async Task DesignTurn_SeededReadAndEdit_CarriesEverySeriesTool()
    {
        var loop = new SurfaceCapturingLoop();
        var context = DesignContext(edit: true);

        await Run(loop, context);

        loop.MasterTools.Should().Contain(Tools);
    }

    [Fact]
    public async Task DesignTurn_ReadWithoutEdit_HasNoEditSeries()
    {
        var loop = new SurfaceCapturingLoop();

        await Run(loop, DesignContext(edit: false));

        loop.MasterTools.Should().Contain("read_series").And.Contain("verify_series").And.NotContain("edit_series");
    }

    [Fact]
    public async Task SeriesTools_NeverWorkedTicket_NoPortSeeded_NotOffered()
    {
        var loop = new SurfaceCapturingLoop();

        await Run(loop, DesignContext(edit: false, read: false));

        loop.MasterTools.Should().NotContain(Tools);
    }

    [Fact]
    public async Task SpecDialogSubAgent_ChildTools_DoNotIncludeSeriesTools()
    {
        var loop = new SurfaceCapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();

        await Run(loop, DesignContext(edit: true), children);

        loop.MasterTools.Should().Contain(Tools);
        children.SeenContexts.Single().ChildTools.Select(t => t.Name).Should().NotContain(Tools, "a child acts on no person's word");
    }

    private static Task Run(
        SurfaceCapturingLoop loop, AgenticMasterContext context, MasterHandlerFixture.StubSubAgentRunner? children = null) =>
        MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(DesignMaster, "body"), null,
                0, subAgents: children ?? new MasterHandlerFixture.StubSubAgentRunner(), limits: new LoopLimitsConfig())
            .ExecuteAsync(context, CancellationToken.None);

    private static AgenticMasterContext DesignContext(bool edit, bool read = true)
    {
        var context = MasterHandlerFixture.BuildContext(DesignMaster, includeTicket: false);
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
        [
            new SpecDialogTurn(SpecDialogTurn.UserRole, "drop the second spec"),
        ]);
        if (read) context.Pipeline.Set(ContextKeys.SpecDialogSeriesRead, Mock.Of<ISeriesRead>());
        if (edit) context.Pipeline.Set(ContextKeys.SpecDialogSeriesEdit, Mock.Of<ISeriesEdit>());
        return context;
    }
}
