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
/// 2026-11-18-9c1cd: every master offers at most 20 tools, each name once, and the same set on every turn of a
/// conversation and every composition of a run — whatever the poster, the memory store, the name or the series.
/// </summary>
public sealed class MasterToolSurfaceTests
{
    private const string Coding = "coding-agent-master";
    private const string Design = "design-partner-master";
    private static readonly DesignSource Figma = new("brand", DesignSourceVendor.Figma, "figma-token");

    private static readonly string[] Retired =
    [
        "multi_edit", "grep_in_file", "list_directory", "get_artifact_credentials", "decide_by_default",
        "dispute_memory", "hand_forward", "run_gate_stage", "wait_for_agents", "compare_reference",
        "view_reference_image", "run_in_reference", "write_mock", "read_mock", "read_notes", "write_notes",
        "delete_file", "state_open_questions", "ticket_work", "ticket_runs", "read_series", "verify_series",
        "derive_specs", "design_read",
    ];

    [Fact]
    public async Task CodingMaster_FullestConfiguration_AtMostTwentyUniqueTools()
    {
        var tools = await CodingSurface(CodingContext());

        tools.Should().OnlyHaveUniqueItems().And.HaveCountLessThanOrEqualTo(20)
            .And.Contain(["finish_spec", "web_fetch", "render_reference", "send_message", "write_context_yaml", "edit"])
            .And.NotContain(Retired).And.NotContain("http_request");
    }

    [Fact]
    public async Task CodingMaster_TwoCompositionsOfOneRun_OfferTheSameTools()
    {
        var context = CodingContext();

        var first = await CodingSurface(context);
        var second = await CodingSurface(context);

        second.Should().Equal(first);
    }

    [Theory]
    [InlineData(Design, true)]
    [InlineData(PipelinePresets.TicketStewardMaster, true)]
    [InlineData(Design, false)]
    public async Task DialogMaster_FullestConfiguration_AtMostTwentyUniqueTools(string master, bool bound)
    {
        var tools = await DialogSurface(DialogContext(master, bound, everything: true));

        tools.Should().OnlyHaveUniqueItems().And.HaveCountLessThanOrEqualTo(20).And.NotContain(Retired);
        tools.Should().Contain(["read_file", "write_file", "edit_file", "run_command", "remember", "recall", "spawn_agents"]);
        if (bound) tools.Should().Contain(["read_ticket", "start_next_attempt"]);
        else tools.Should().NotContain("read_ticket");
    }

    [Theory]
    [InlineData(Design, true)]
    [InlineData(Design, false)]
    [InlineData(PipelinePresets.TicketStewardMaster, true)]
    public async Task DialogMaster_TwoTurnsOfOneConversation_OfferTheSameTools(string master, bool bound)
    {
        // Named or not, with or without a memory index, a poster with or without runs.control, a filing or none.
        var full = await DialogSurface(DialogContext(master, bound, everything: true));
        var bare = await DialogSurface(DialogContext(master, bound, everything: false));

        bare.Should().BeEquivalentTo(full);
    }

    private static async Task<IReadOnlyList<string>> CodingSurface(AgenticMasterContext context)
    {
        var loop = new SurfaceCapturingLoop();
        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(Coding, "body"), null, 20)
            .ExecuteAsync(context, CancellationToken.None);
        return loop.MasterTools;
    }

    private static AgenticMasterContext CodingContext()
    {
        var context = MasterHandlerFixture.BuildContext(Coding);
        context.Pipeline.Set(ContextKeys.RunId, "run-9c1cd");
        context.Pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject
        {
            Name = "p", DesignSources = [Figma], Sandbox = new SandboxConfig { Browser = new ProjectBrowserConfig { Enabled = true } },
        });
        context.Pipeline.Set(ContextKeys.CurrentSpec,
            new SpecDraft("2026-11-18-0000a", "build the checkout", "spec: 2026-11-18-0000a\ngoal: build the checkout\n", []));
        return context;
    }

    private static async Task<IReadOnlyList<string>> DialogSurface(AgenticMasterContext context)
    {
        var loop = new SurfaceCapturingLoop();
        await BoundDialogTurn.Run(loop, context);
        return loop.MasterTools;
    }

    private static AgenticMasterContext DialogContext(string master, bool bound, bool everything)
    {
        var context = BoundDialogTurn.Context(master);
        var pipeline = context.Pipeline;
        pipeline.Set(ContextKeys.SpecDialogReplySlot, new SpecDialogReplySlot());
        pipeline.Set<IReadOnlyList<DesignSource>>(ContextKeys.SpecDialogDesignSources, [Figma]);
        pipeline.Set(ContextKeys.SpecDialogProject, new ResolvedProject { Name = "p" });
        if (!bound)
            foreach (var key in new[] { ContextKeys.SpecDialogTicketReader, ContextKeys.SpecDialogTicketRuns,
                         ContextKeys.SpecDialogAttemptStart, ContextKeys.SpecDialogSeriesRead, ContextKeys.SpecDialogSeriesEdit })
                pipeline.Remove(key);
        if (everything)
        {
            pipeline.Set(ContextKeys.SpecDialogNameable, true);
            pipeline.Set(ContextKeys.MemoryIndex, "- [a](a.md) — a fact");
            pipeline.Set(ContextKeys.MayStartRuns, true);
            if (bound) pipeline.Set(ContextKeys.SpecDialogTicketWork, Mock.Of<IBoundTicketWork>());
        }
        else
        {
            pipeline.Remove(ContextKeys.SpecDialogWithdrawal);
            pipeline.Remove(ContextKeys.SpecDialogAttemptStart);
            pipeline.Remove(ContextKeys.SpecDialogSeriesEdit);
        }
        return context;
    }
}
