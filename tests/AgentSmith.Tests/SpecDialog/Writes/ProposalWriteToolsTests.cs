using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog.Writes;

public sealed class ProposalWriteToolsTests
{
    [Fact]
    public void ProposalWriteTools_UnboundTurn_OffersEveryWriteButTheComment()
    {
        var tools = Handlers.MasterHandlerFixture.ProposalWrites().For(new PipelineContext(), new ProposalStore());

        tools.Select(t => t.Name).Should().BeEquivalentTo(
            ["write_spec", "write_contract", "write_ticket", "write_bug", "remove_item"]);
    }

    [Fact]
    public async Task WriteTicket_Valid_IsStoredAndTheAnswerSaysWhatTheDraftHolds()
    {
        var store = new ProposalStore();
        var tool = Handlers.MasterHandlerFixture.ProposalWrites().For(new PipelineContext(), store)
            .OfType<AIFunction>().Single(t => t.Name == TicketWriteToolHost.Name);

        var answer = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["key"] = "a-one", ["title"] = "A", ["requirement"] = "Do A.", ["done"] = new[] { "A is done" },
        });

        answer!.ToString().Should().Be("Stored ticket 'a-one'. The draft holds 1 ticket(s).");
        store.Changed.Should().BeTrue();
    }

    /// <summary>2026-11-12-681f: DialogDraftRepos reads a top-level repos key, so the schema the model writes from names it.</summary>
    [Fact]
    public void WriteSpec_Schema_NamesTheReposKeyTheScopeCheckReads()
    {
        var tool = Handlers.MasterHandlerFixture.ProposalWrites().For(new PipelineContext(), new ProposalStore())
            .OfType<AIFunction>().Single(t => t.Name == SpecWriteToolHost.Name);

        tool.JsonSchema.ToString().Should().Contain("repos: the one repository it changes");
    }

    [Fact]
    public void TicketGraphChecker_Ticket_BadKey_IsNamed()
    {
        var checker = new Application.Services.SpecDialog.TicketGraphChecker(new Application.Services.SpecDialog.TicketGraphOrderer());

        checker.Ticket(DraftWriteRig.Ticket("Bad Key"), new HashSet<string>(), []).Should().Contain("has the key 'Bad Key'");
    }
}
