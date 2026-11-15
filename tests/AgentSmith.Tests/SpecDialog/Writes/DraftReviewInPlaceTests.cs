using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog.Writes;

/// <summary>
/// 2026-11-15-06fc: a spec revised at its draft path keeps its id, so the review ledger keeps every other text it
/// reviewed and sends only the changed one back; a text a pass passed stays settled when the whole file is rewritten.
/// </summary>
public sealed class DraftReviewInPlaceTests
{
    [Fact]
    public async Task EditFile_OneRefusedSpec_OnlyThatTextGoesBackToReview()
    {
        var (store, ledger) = await ReviewedEpicAsync(SpecReviewVerdicts.Refuse);
        var p0102 = Child(store, "p0102");

        await InvokeAsync(store, "edit_file", ("path", "draft:specs/p0101.yaml"), ("old_string", "One"), ("new_string", "One, revised"));

        ledger.For(Child(store, "p0102")).Should().NotBeNull("p0102's text is the one the pass reviewed");
        Child(store, "p0102").Should().Be(p0102);
        ledger.For(Child(store, "p0101")).Should().BeNull("p0101's text changed under its id");
        ledger.Covers(ProposalDraftOutcome.For(store)).Should().BeFalse("p0101 was refused and its new text is unreviewed");
    }

    [Fact]
    public async Task WriteFile_WholePassedSpecAtItsPath_StaysSettled()
    {
        var (store, ledger) = await ReviewedEpicAsync(SpecReviewVerdicts.BuildWithCuts);

        await InvokeAsync(store, "write_file", ("path", "draft:specs/p0101.yaml"), ("content", "goal: \"One, cuts applied\""));

        Child(store, "p0101").SpecId.Should().Be("p0101");
        ledger.Covers(ProposalDraftOutcome.For(store)).Should().BeTrue("a passed id is settled whatever its text becomes");
    }

    private static async Task<(ProposalStore Store, ProposalReviewLedger Ledger)> ReviewedEpicAsync(string verdict)
    {
        var store = new ProposalStore();
        await InvokeAsync(store, "write_file", ("path", "draft:epic/p0100.yaml"), ("content", "goal: \"The epic\""));
        await InvokeAsync(store, "write_file", ("path", "draft:specs/p0101.yaml"), ("content", "goal: \"One\""));
        await InvokeAsync(store, "write_file", ("path", "draft:specs/p0102.yaml"), ("content", "goal: \"Two\""));
        var ledger = ProposalReviewLedger.Of(new PipelineContext());
        ledger.Record(ProposalDraftOutcome.For(store) with { ReviewRounds = [Round("p0101", verdict), Round("p0102", verdict)] });
        ledger.Covers(ProposalDraftOutcome.For(store)).Should().BeTrue();
        return (store, ledger);
    }

    private static SpecDraft Child(ProposalStore store, string id) => store.Current.Specs.Single(s => s.Draft.SpecId == id).Draft;

    private static ProposalReviewRound Round(string id, string verdict) => new(id, 1, verdict, [verdict], [], [], []);

    private static async Task InvokeAsync(ProposalStore store, string name, params (string Key, object Value)[] arguments)
    {
        var tool = Handlers.MasterHandlerFixture.ProposalWrites().For(new PipelineContext(), store)
            .OfType<AIFunction>().Single(t => t.Name == name);
        var args = new AIFunctionArguments();
        foreach (var (key, value) in arguments) args[key] = value;
        (await tool.InvokeAsync(args))!.ToString().Should().NotStartWith("Not stored");
    }
}
