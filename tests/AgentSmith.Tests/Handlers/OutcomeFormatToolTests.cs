using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-11-05-862eb: the design master reads an outcome's format from the catalog's references through
/// outcome_format — offered on every design turn with a reply slot, never on the steward's surface; a catalog
/// without the reference (the v5.23.0 pin) points the master at its own instructions.
/// </summary>
public sealed class OutcomeFormatToolTests
{
    private static readonly Dictionary<string, string> Catalog = new(StringComparer.Ordinal)
    {
        ["design-outcome-spec"] = "## Drafting a spec\n",
        ["design-outcome-epic"] = "## Proposing an epic\n",
        ["writing-documents"] = "A document is written whole.\n",
    };

    [Fact]
    public async Task OutcomeFormat_Epic_ReturnsSpecThenEpicFormat()
    {
        var answer = await Invoke(Catalog, "Epic");

        answer.Should().Be("## Drafting a spec\n\n## Proposing an epic", "each child of an epic is a spec");
    }

    /// <summary>2026-11-10-14a3f: one host serves one turn, and a format it returned stands above.</summary>
    [Fact]
    public async Task OutcomeFormat_SameKindTwiceInATurn_ReturnsItOnce()
    {
        var tool = new OutcomeFormatToolHost(new References(Catalog)).GetTools(null, null).Single();

        var first = await Call(tool, "spec");
        var again = await Call(tool, "spec");
        var epic = await Call(tool, "epic");

        first.Should().Be("## Drafting a spec");
        again.Should().StartWith("The spec format was returned earlier in this turn");
        epic.Should().Be("## Proposing an epic", "the spec part of an epic's format was already given");
    }

    private static async Task<string> Call(AIFunction tool, string kind) =>
        (await tool.InvokeAsync(new AIFunctionArguments { ["kind"] = kind }))!.ToString()!;

    [Fact]
    public async Task OutcomeFormat_Document_ReturnsWritingDocuments()
    {
        var answer = await Invoke(new() { ["writing-documents"] = "A document is written whole.\n" }, "document");

        answer.Should().Be("A document is written whole.", "v5.23.0 already ships writing-documents");
    }

    [Fact]
    public async Task OutcomeFormat_UnknownKind_NamesTheValidKinds()
    {
        var answer = await Invoke(Catalog, "migration");

        answer.Should().Be(
            "There is no format for 'migration'. The kinds are: spec, bug, epic, tickets, comment, document.");
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("bug")]
    [InlineData("tickets")]
    [InlineData("comment")]
    public async Task OutcomeFormat_CatalogWithoutReference_PointsAtTheMastersInstructions(string kind)
    {
        var answer = await Invoke(new() { ["writing-documents"] = "A document is written whole.\n" }, kind);

        answer.Should().Be(OutcomeFormatToolHost.InOwnInstructions)
            .And.Contain("formats are in your own instructions");
    }

    [Fact]
    public void TicketActToolsFactory_DesignTurn_OffersOutcomeFormat()
    {
        var factory = new TicketActToolsFactory(MasterHandlerFixture.Verification(), new References(Catalog), MasterHandlerFixture.ProposalWrites());
        var seeded = Context();
        seeded.Pipeline.Set(ContextKeys.SpecDialogReplySlot, new SpecDialogReplySlot());

        factory.Tools(seeded).Select(t => t.Name).Should().Contain(OutcomeFormatToolHost.Name);
        factory.Tools(Context()).Select(t => t.Name).Should().NotContain(OutcomeFormatToolHost.Name,
            "a run that is no design turn drafts no outcome block");
    }

    [Fact]
    public void TicketStewardTools_StewardSurface_DropsOutcomeFormat()
    {
        IList<AITool> master = [.. new OutcomeFormatToolHost(new References(Catalog)).GetTools(null, null),
            .. new NameConversationToolHost(new SpecDialogReplySlot()).GetTools(null, null)];

        TicketStewardTools.Of(master).Select(t => t.Name).Should()
            .NotContain(OutcomeFormatToolHost.Name).And.Contain(NameConversationToolHost.Name);
    }

    private static async Task<string> Invoke(Dictionary<string, string> catalog, string kind) =>
        (await new OutcomeFormatToolHost(new References(catalog)).GetTools(null, null).Single().InvokeAsync(
            new AIFunctionArguments { ["kind"] = kind }))!.ToString()!;

    private static AgenticMasterContext Context() => new(
        "design-partner-master", new Repository(new BranchName("main"), "/tmp"), string.Empty,
        new AgentConfig { Type = "claude" }, new PipelineContext());

    private sealed class References(Dictionary<string, string> bySlug) : ISkillReferenceSource
    {
        public string? TryRead(string slug) => bySlug.GetValueOrDefault(slug);
    }
}
