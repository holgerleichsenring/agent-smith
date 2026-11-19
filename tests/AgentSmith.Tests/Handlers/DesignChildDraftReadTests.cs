using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-11-19-1de4: a child of a design turn reads the draft at draft: paths and the conversation's notes at notes:,
/// as its master does — the current draft, written by the master before the spawn — and still writes nothing.
/// </summary>
public sealed class DesignChildDraftReadTests
{
    private const string DesignMaster = "design-partner-master";
    private const string Ticket = "title: \"Export orders\"\nrequirement: \"Export the orders as CSV.\"\ndone:\n  - \"a CSV downloads\"\n";

    [Fact]
    public async Task Child_ReadsTheDraftTheMasterWroteBeforeTheSpawn()
    {
        var (child, _) = await ChildOfADesignTurn();

        (await Invoke(child, "read_file", ("path", "draft:tickets/export-orders.yaml"), ("with_line_numbers", false)))
            .Should().Contain("Export the orders as CSV.");
        (await Invoke(child, "directory_tree", ("root", "draft:"))).Should().Contain("draft:tickets/export-orders.yaml");
    }

    [Fact]
    public async Task Child_ReadsTheConversationNotes()
    {
        var (child, _) = await ChildOfADesignTurn();

        (await Invoke(child, "read_file", ("path", "notes:"))).Should().Contain("the reviewer reads the draft");
    }

    [Fact]
    public async Task Child_IsOfferedNoWriteTool()
    {
        var (child, _) = await ChildOfADesignTurn();

        child.Select(t => t.Name).Should().NotContain(["write_file", "edit_file", "delete_file", "run_command"]);
    }

    private static async Task<(IReadOnlyList<AIFunction> Child, SpecDialogReplySlot Slot)> ChildOfADesignTurn()
    {
        var slot = new SpecDialogReplySlot();
        var context = MasterHandlerFixture.BuildContext(DesignMaster, includeTicket: false);
        context.Pipeline.Set(ContextKeys.RunId, "2026-11-19T1200-design");
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set(ContextKeys.DialogueJobId, "s-design");
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
            [new SpecDialogTurn(SpecDialogTurn.UserRole, "draft the export ticket")]);
        context.Pipeline.Set(ContextKeys.SpecDialogReplySlot, slot);
        context.Pipeline.Set<IConversationNotes>(ContextKeys.SpecDialogNotesStore, new InMemoryNotes());
        var children = new MasterHandlerFixture.StubSubAgentRunner();

        await MasterHandlerFixture.Build(new WriteThenSpawnLoop(), new MasterHandlerFixture.StubPromptCatalog(DesignMaster, "body"),
                null, 4, subAgents: children, limits: new LoopLimitsConfig())
            .ExecuteAsync(context, CancellationToken.None);

        return ([.. children.SeenContexts.Single().ChildTools.OfType<AIFunction>()], slot);
    }

    private static async Task<string> Invoke(IReadOnlyList<AIFunction> tools, string name, params (string Key, object? Value)[] args)
    {
        var arguments = new AIFunctionArguments();
        foreach (var (key, value) in args) arguments[key] = value;
        return (await tools.Single(t => t.Name == name).InvokeAsync(arguments))?.ToString() ?? string.Empty;
    }

    /// <summary>The master writes a ticket and its notes, then spawns one reviewer.</summary>
    private sealed class WriteThenSpawnLoop : IAgenticLoopRunner
    {
        private const string OneTask = "[{\"name\":\"Reviewer\",\"activity\":\"review the draft\","
            + "\"task_description\":\"read draft:tickets/export-orders.yaml\",\"inherited_context\":"
            + "{\"pipeline_goal\":\"review the draft\",\"prior_context_slice\":\"the transcript\"}}]";
        private bool _done;

        public async Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            if (!_done)
            {
                _done = true;
                var tools = request.Tools!.OfType<AIFunction>().ToList();
                var write = tools.Single(t => t.Name == "write_file");
                (await write.InvokeAsync(new AIFunctionArguments { ["path"] = "draft:tickets/export-orders.yaml", ["content"] = Ticket }, cancellationToken))
                    !.ToString().Should().StartWith("Stored draft:tickets/export-orders.yaml");
                (await write.InvokeAsync(new AIFunctionArguments { ["path"] = "notes:", ["content"] = "the reviewer reads the draft" }, cancellationToken))
                    !.ToString().Should().StartWith("Stored");
                await tools.Single(t => t.Name == "spawn_agents").InvokeAsync(
                    new AIFunctionArguments { ["tasks"] = JsonDocument.Parse(OneTask).RootElement }, cancellationToken);
            }
            return new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "An answer with no outcome block."))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
                },
                TimeSpan.FromSeconds(1));
        }
    }

    private sealed class InMemoryNotes : IConversationNotes
    {
        private string? _text;

        public Task<string?> ReadAsync(string sessionId, CancellationToken cancellationToken) => Task.FromResult(_text);

        public Task WriteAsync(string sessionId, string notes, CancellationToken cancellationToken)
        {
            _text = notes;
            return Task.CompletedTask;
        }
    }
}
