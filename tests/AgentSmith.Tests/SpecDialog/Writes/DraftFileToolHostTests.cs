using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog.Writes;

/// <summary>2026-11-15-06fc: the design master's draft as files — write_file, edit_file, delete_file over the store.</summary>
public sealed class DraftFileToolHostTests
{
    private const string Spec = "goal: \"Widgets are stored\"\ndone:\n  - \"a widget survives a restart\"";

    [Fact]
    public void ProposalWriteTools_OffersTheThreeFileTools()
    {
        var tools = Handlers.MasterHandlerFixture.ProposalWrites().For(new PipelineContext(), new ProposalStore());

        tools.Select(t => t.Name).Should().BeEquivalentTo(["write_file", "edit_file", "delete_file"]);
    }

    [Fact]
    public async Task WriteFile_Spec_IsStoredUnderItsFileNameAndSaysWhatTheChecksAdded()
    {
        var store = new ProposalStore();

        var answer = await InvokeAsync(store, "write_file", ("path", "draft:specs/p0101.yaml"), ("content", Spec));

        answer.Should().StartWith("Stored draft:specs/p0101.yaml. The draft holds one spec.")
            .And.Contain("The checks changed what you wrote").And.Contain("spec: p0101");
        store.Current.Specs.Single().Draft.SpecId.Should().Be("p0101");
        store.Changed.Should().BeTrue();
    }

    [Fact]
    public async Task EditFile_Spec_RevisesInPlaceAndKeepsTheId()
    {
        var store = new ProposalStore();
        await InvokeAsync(store, "write_file", ("path", "specs/p0101.yaml"), ("content", Spec));

        var answer = await InvokeAsync(store, "edit_file", ("path", "draft:specs/p0101.yaml"),
            ("old_string", "Widgets are stored"), ("new_string", "Widgets are stored and listed"));

        answer.Should().Be("Stored draft:specs/p0101.yaml. The draft holds one spec.");
        store.Current.Specs.Single().Draft.Should().Match<SpecDraft>(d => d.SpecId == "p0101" && d.Goal == "Widgets are stored and listed");
    }

    [Theory]
    [InlineData("a widget", "appears multiple times")]
    [InlineData("no such text", "old_string not found in draft:specs/p0101.yaml")]
    public async Task EditFile_OldStringNotOnceInTheFile_StoresNothing(string oldString, string reason)
    {
        var store = new ProposalStore();
        await InvokeAsync(store, "write_file", ("path", "draft:specs/p0101.yaml"),
            ("content", Spec + "\nassumptions:\n  - \"a widget is small\""));
        var before = store.Current;

        var answer = await InvokeAsync(store, "edit_file", ("path", "draft:specs/p0101.yaml"), ("old_string", oldString), ("new_string", "x"));

        answer.Should().StartWith("Not stored:").And.Contain(reason);
        store.Current.Should().BeSameAs(before);
    }

    [Fact]
    public async Task EditFile_SameText_StoresNothingSoTheOfferStands()
    {
        var slot = new SpecDialogReplySlot();
        await InvokeAsync(slot.Proposal, "write_file", ("path", "draft:specs/p0101.yaml"), ("content", Spec));
        await new OfferFilingToolHost(slot).GetTools(null, null).Single().InvokeAsync(new AIFunctionArguments { ["line"] = "Files it." });

        var answer = await InvokeAsync(slot.Proposal, "edit_file", ("path", "draft:specs/p0101.yaml"),
            ("old_string", "stored"), ("new_string", "stored"));

        answer.Should().Contain("is unchanged");
        slot.OfferedLine.Should().Be("Files it.");
    }

    [Fact]
    public async Task EditFile_AfterTheOffer_WithdrawsIt()
    {
        var slot = new SpecDialogReplySlot();
        await InvokeAsync(slot.Proposal, "write_file", ("path", "draft:specs/p0101.yaml"), ("content", Spec));
        await new OfferFilingToolHost(slot).GetTools(null, null).Single().InvokeAsync(new AIFunctionArguments { ["line"] = "Files it." });

        await InvokeAsync(slot.Proposal, "edit_file", ("path", "draft:specs/p0101.yaml"), ("old_string", "stored"), ("new_string", "kept"));

        slot.OfferedLine.Should().BeNull("the offer named the draft before the edit");
    }

    [Fact]
    public async Task DeleteFile_SpecAnotherRequires_IsRefused()
    {
        var store = await EpicAsync();

        var answer = await InvokeAsync(store, "delete_file", ("path", "draft:specs/p0101.yaml"));

        answer.Should().StartWith("Not stored:").And.Contain("'p0102' require 'p0101' — edit or delete them first");
        store.Current.Specs.Should().HaveCount(3);
    }

    [Fact]
    public async Task DeleteFile_TheWholeDraft_ThenATicket_SwitchesKind()
    {
        var store = await EpicAsync();

        (await InvokeAsync(store, "delete_file", ("path", "draft:"))).Should().StartWith("Deleted draft:.");
        var answer = await InvokeAsync(store, "write_file", ("path", "draft:tickets/a-one.yaml"),
            ("content", "title: A\nrequirement: Do A.\ndone:\n  - A is done\n"));

        answer.Should().StartWith("Stored draft:tickets/a-one.yaml. The draft holds 1 ticket(s).");
    }

    [Fact]
    public async Task WriteFile_OutsideTheLayout_IsRefusedWithTheLayout()
    {
        var store = new ProposalStore();

        var answer = await InvokeAsync(store, "write_file", ("path", "draft:notes.md"), ("content", "x"));

        answer.Should().StartWith("Not stored: draft:notes.md is outside the draft's layout").And.Contain(DraftPath.Layout);
        store.Changed.Should().BeFalse();
    }

    /// <summary>2026-11-12-681f: DialogDraftRepos reads a top-level repos key, so the format the model writes from names it.</summary>
    [Fact]
    public void WriteFile_Format_NamesTheReposKeyTheScopeCheckReads()
    {
        var tool = Handlers.MasterHandlerFixture.ProposalWrites().For(new PipelineContext(), new ProposalStore())
            .OfType<AIFunction>().Single(t => t.Name == DraftFileToolHost.WriteName);

        tool.JsonSchema.ToString().Should().Contain("repos: the one repository it changes").And.Contain("requirement (its scope in prose");
    }

    private static async Task<ProposalStore> EpicAsync()
    {
        var store = new ProposalStore();
        await InvokeAsync(store, "write_file", ("path", "draft:epic/p0100.yaml"), ("content", "goal: \"The epic\""));
        await InvokeAsync(store, "write_file", ("path", "draft:specs/p0101.yaml"), ("content", "goal: \"One\""));
        await InvokeAsync(store, "write_file", ("path", "draft:specs/p0102.yaml"), ("content", "goal: \"Two\"\nrequires: [p0101]"));
        store.Current.Specs.Should().HaveCount(3);
        return store;
    }

    private static async Task<string> InvokeAsync(ProposalStore store, string name, params (string Key, object Value)[] arguments)
    {
        var tool = Handlers.MasterHandlerFixture.ProposalWrites().For(new PipelineContext(), store)
            .OfType<AIFunction>().Single(t => t.Name == name);
        var args = new AIFunctionArguments();
        foreach (var (key, value) in arguments) args[key] = value;
        return (await tool.InvokeAsync(args))!.ToString()!;
    }
}
