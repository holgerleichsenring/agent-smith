using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.Specs;
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
    public async Task WriteFile_Spec_IsStoredUnderItsFileName()
    {
        var store = new ProposalStore();

        var answer = await InvokeAsync(store, "write_file", ("path", "draft:specs/store.yaml"), ("content", Spec));

        answer.Should().Be("Stored draft:specs/store.yaml. The draft holds one spec.", "stating the spec's own name is no change worth a line");
        store.Current.Specs.Single().Draft.SpecId.Should().Be("store");
        store.Changed.Should().BeTrue();
    }

    [Fact]
    public async Task WriteFile_Ticket_IsStoredAsWritten()
    {
        var store = new ProposalStore();
        const string written = "{\"title\": \"A\", \"requirement\": \"Do A.\", \"done\": [\"A is done\"]}";

        var answer = await InvokeAsync(store, "write_file", ("path", "draft:tickets/a-one.yaml"), ("content", written));

        answer.Should().Be("Stored draft:tickets/a-one.yaml. The draft holds 1 ticket(s).");
        DraftFiles.Find(store.Current, "tickets/a-one.yaml")!.Text.Should().Be(written + "\n");
    }

    /// <summary>2026-11-15-67f1: the replay's defect — five of six ticket edits quoted the text written and found nothing.</summary>
    [Fact]
    public async Task EditFile_Ticket_QuoteFromTheWrittenText_IsStored()
    {
        var store = new ProposalStore();
        await InvokeAsync(store, "write_file", ("path", "draft:tickets/a-one.yaml"),
            ("content", "title: \"Export: orders\"\nrequirement: >\n  Export the orders\n  as CSV.\ndone:\n  - \"WHEN export is clicked THEN a CSV downloads\"\n"));

        var answer = await InvokeAsync(store, "edit_file", ("path", "draft:tickets/a-one.yaml"),
            ("old_string", "  Export the orders\n  as CSV."), ("new_string", "  Export the orders\n  as CSV and JSON."));

        answer.Should().Be("Stored draft:tickets/a-one.yaml. The draft holds 1 ticket(s).");
        store.Current.Tickets.Single().Requirement.Should().Be("Export the orders as CSV and JSON.");
    }

    [Fact]
    public async Task EditFile_Bug_QuoteFromTheWrittenText_IsStored()
    {
        var store = new ProposalStore();
        await InvokeAsync(store, "write_file", ("path", "draft:bug.yaml"),
            ("content", "title: \"Null on empty\"\ndescription: |\n  It throws\n  when the list is empty.\nacceptance_criteria:\n  - \"an empty list shows nothing\"\n"));

        var answer = await InvokeAsync(store, "edit_file", ("path", "draft:bug.yaml"),
            ("old_string", "  - \"an empty list shows nothing\""), ("new_string", "  - \"an empty list shows 'no items'\""));

        answer.Should().StartWith("Stored draft:bug.yaml.");
        store.Current.Bug!.AcceptanceCriteria.Should().Be("- an empty list shows 'no items'");
    }

    [Fact]
    public async Task EditFile_Contract_QuoteFromTheWrittenText_IsStored()
    {
        var store = await EpicAsync();
        await InvokeAsync(store, "write_file", ("path", "draft:contract.yaml"),
            ("content", "areas:\n  - spec: store\n    paths: [\"src/store/\"]\n"));

        var answer = await InvokeAsync(store, "edit_file", ("path", "draft:contract.yaml"),
            ("old_string", "paths: [\"src/store/\"]"), ("new_string", "paths: [\"src/store/\", \"src/db/\"]"));

        answer.Should().StartWith("Stored draft:contract.yaml.");
        store.Current.Contract!.Areas.Single().Paths.Should().Equal("src/store/", "src/db/");
    }

    [Fact]
    public async Task EditFile_Spec_RevisesInPlaceAndKeepsTheId()
    {
        var store = new ProposalStore();
        await InvokeAsync(store, "write_file", ("path", "specs/store.yaml"), ("content", Spec));

        var answer = await InvokeAsync(store, "edit_file", ("path", "draft:specs/store.yaml"),
            ("old_string", "Widgets are stored"), ("new_string", "Widgets are stored and listed"));

        answer.Should().Be("Stored draft:specs/store.yaml. The draft holds one spec.");
        store.Current.Specs.Single().Draft.Should().Match<SpecDraft>(d => d.SpecId == "store" && d.Goal == "Widgets are stored and listed");
    }

    [Theory]
    [InlineData("a widget", "appears multiple times")]
    [InlineData("no such text", "old_string not found in draft:specs/store.yaml")]
    public async Task EditFile_OldStringNotOnceInTheFile_StoresNothing(string oldString, string reason)
    {
        var store = new ProposalStore();
        await InvokeAsync(store, "write_file", ("path", "draft:specs/store.yaml"),
            ("content", Spec + "\nassumptions:\n  - \"a widget is small\""));
        var before = store.Current;

        var answer = await InvokeAsync(store, "edit_file", ("path", "draft:specs/store.yaml"), ("old_string", oldString), ("new_string", "x"));

        answer.Should().StartWith("Not stored:").And.Contain(reason);
        store.Current.Should().BeSameAs(before);
    }

    [Fact]
    public async Task EditFile_SameText_StoresNothingSoTheOfferStands()
    {
        var slot = new SpecDialogReplySlot();
        await InvokeAsync(slot.Proposal, "write_file", ("path", "draft:specs/store.yaml"), ("content", Spec));
        await new OfferFilingToolHost(slot).GetTools(null, null).Single().InvokeAsync(new AIFunctionArguments { ["line"] = "Files it." });

        var answer = await InvokeAsync(slot.Proposal, "edit_file", ("path", "draft:specs/store.yaml"),
            ("old_string", "stored"), ("new_string", "stored"));

        answer.Should().Contain("is unchanged");
        slot.OfferedLine.Should().Be("Files it.");
    }

    [Fact]
    public async Task EditFile_AfterTheOffer_WithdrawsIt()
    {
        var slot = new SpecDialogReplySlot();
        await InvokeAsync(slot.Proposal, "write_file", ("path", "draft:specs/store.yaml"), ("content", Spec));
        await new OfferFilingToolHost(slot).GetTools(null, null).Single().InvokeAsync(new AIFunctionArguments { ["line"] = "Files it." });

        await InvokeAsync(slot.Proposal, "edit_file", ("path", "draft:specs/store.yaml"), ("old_string", "stored"), ("new_string", "kept"));

        slot.OfferedLine.Should().BeNull("the offer named the draft before the edit");
    }

    [Fact]
    public async Task DeleteFile_SpecAnotherRequires_IsRefused()
    {
        var store = await EpicAsync();

        var answer = await InvokeAsync(store, "delete_file", ("path", "draft:specs/store.yaml"));

        answer.Should().StartWith("Not stored:").And.Contain("'api' require 'store' — edit or delete them first");
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

    /// <summary>2026-11-19-3d7e: a stored spec answers the evidence its facts cite that does not resolve.</summary>
    [Fact]
    public async Task WriteFile_SpecCitingAMissingFile_AnswersTheFinding()
    {
        var (pipeline, _) = WithRepository();

        var answer = await InvokeAsync(MasterHandlerFixtureWrites(), pipeline, new ProposalStore(), "write_file",
            ("path", "draft:specs/store.yaml"), ("content", Cited("repo-a/src/gone.cs:3")));

        answer.Should().StartWith("Stored draft:specs/store.yaml.").And.Contain(DraftWriteFindings.Heading)
            .And.Contain($"- {AgentSmith.Application.Services.SpecDialog.ProposalEvidenceReview.Problem}: ")
            .And.Contain("repo-a/src/gone.cs").And.Contain("(claim: \"the store exists\")");
    }

    [Fact]
    public async Task WriteFile_SpecCitingAnExistingFile_AnswersNoFinding()
    {
        var (pipeline, repo) = WithRepository();

        var answer = await InvokeAsync(MasterHandlerFixtureWrites(), pipeline, new ProposalStore(), "write_file",
            ("path", "draft:specs/store.yaml"), ("content", Cited("repo-a/src/a.cs:1-2")));

        answer.Should().StartWith("Stored draft:specs/store.yaml.").And.NotContain(DraftWriteFindings.Heading);
        repo.Ran.Should().NotBeEmpty("the citation was read");
    }

    [Fact]
    public async Task EditFile_TicketRequiringAMissingTicket_AnswersTheFinding()
    {
        var existence = new MissingTickets("41");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogProject,
            new ResolvedProject { Name = "shop", Tracker = new TrackerConnection { Name = "gh-tracker" } });
        var writes = Handlers.MasterHandlerFixture.ProposalWrites(existence);
        var store = new ProposalStore();
        await InvokeAsync(writes, pipeline, store, "write_file", ("path", "draft:tickets/a-one.yaml"),
            ("content", "title: A\nrequirement: Do A.\ndone:\n  - A is done\n"));

        var answer = await InvokeAsync(writes, pipeline, store, "edit_file", ("path", "draft:tickets/a-one.yaml"),
            ("old_string", "requirement: Do A."), ("new_string", "requirement: Do A.\nrequires: [\"ticket:41\"]"));

        answer.Should().StartWith("Stored draft:tickets/a-one.yaml.").And.Contain(DraftWriteFindings.Heading)
            .And.Contain("- required ticket not found: ticket 41 does not exist on gh-tracker");
        existence.Checked.Should().Equal("41");
    }

    [Fact]
    public async Task WriteFile_Refused_RunsNoCheck()
    {
        var (pipeline, repo) = WithRepository();
        var existence = new MissingTickets("41");
        var writes = Handlers.MasterHandlerFixture.ProposalWrites(existence);

        var spec = await InvokeAsync(writes, pipeline, new ProposalStore(), "write_file",
            ("path", "draft:notes.md"), ("content", Cited("repo-a/src/gone.cs:3")));
        var ticket = await InvokeAsync(writes, pipeline, new ProposalStore(), "write_file",
            ("path", "draft:tickets/a-one.yaml"), ("content", "title: A\nrequires: [\"ticket:41\"]\n"));

        spec.Should().StartWith("Not stored:").And.NotContain(DraftWriteFindings.Heading);
        ticket.Should().StartWith("Not stored:").And.NotContain(DraftWriteFindings.Heading);
        repo.Ran.Should().BeEmpty();
        existence.Checked.Should().BeEmpty();
    }

    private static string Cited(string evidence) =>
        Spec + $"\nfacts:\n  - claim: \"the store exists\"\n    evidence: \"{evidence}\"";

    private static ProposalWriteTools MasterHandlerFixtureWrites() => Handlers.MasterHandlerFixture.ProposalWrites();

    private static (PipelineContext Pipeline, EvidenceTreeSandbox Repo) WithRepository()
    {
        var repo = new EvidenceTreeSandbox(new Dictionary<string, string> { ["src/a.cs"] = "1\n2\n" });
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)new Dictionary<string, ISandbox> { ["repo-a"] = repo });
        return (pipeline, repo);
    }

    /// <summary>A tracker on which the given ids do not exist; records every id checked.</summary>
    private sealed class MissingTickets(params string[] missing) : ITicketExistence
    {
        public List<string> Checked { get; } = [];

        public Task<TicketExistenceResult> CheckAsync(ResolvedProject project, IReadOnlyList<string> ids, CancellationToken ct)
        {
            Checked.AddRange(ids);
            return Task.FromResult(new TicketExistenceResult([.. missing.Where(ids.Contains)], []));
        }
    }

    private static async Task<ProposalStore> EpicAsync()
    {
        var store = new ProposalStore();
        await InvokeAsync(store, "write_file", ("path", "draft:epic/platform.yaml"), ("content", "goal: \"The epic\""));
        await InvokeAsync(store, "write_file", ("path", "draft:specs/store.yaml"), ("content", "goal: \"One\""));
        await InvokeAsync(store, "write_file", ("path", "draft:specs/api.yaml"), ("content", "goal: \"Two\"\nrequires: [store]"));
        store.Current.Specs.Should().HaveCount(3);
        return store;
    }

    private static Task<string> InvokeAsync(ProposalStore store, string name, params (string Key, object Value)[] arguments) =>
        InvokeAsync(Handlers.MasterHandlerFixture.ProposalWrites(), new PipelineContext(), store, name, arguments);

    private static async Task<string> InvokeAsync(
        ProposalWriteTools writes, PipelineContext pipeline, ProposalStore store, string name, params (string Key, object Value)[] arguments)
    {
        var tool = writes.For(pipeline, store).OfType<AIFunction>().Single(t => t.Name == name);
        var args = new AIFunctionArguments();
        foreach (var (key, value) in arguments) args[key] = value;
        return (await tool.InvokeAsync(args))!.ToString()!;
    }
}
