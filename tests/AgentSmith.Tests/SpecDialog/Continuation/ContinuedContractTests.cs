using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.Specs.Contract;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-8762j: a continuation records the proposal's own contract, re-keyed onto the continued ids with the
/// map that gave the drafts theirs; the basis record's contract only when the proposal carries none.
/// </summary>
public sealed class ContinuedContractTests : IDisposable
{
    private const string Series = "2026-10-13-ab12";
    private static readonly string A = Series + "a", B = Series + "b", C = Series + "c", D = Series + "d";

    private readonly ContinuationHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TicketContinuationAct_EpicContract_RekeyedOntoContinuedIds()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B, C], executed: [A]);
        var proposal = new EpicOutcome(Draft("parent", "The widget is reliable"),
            [Draft(B, "B, re-cut"), Draft("new-one", "Report every drop")],
            SeriesContractFixtures.Of(
                names: [new("DropReported", "event", 90, "new-one", [B]), new("Widget", "type", null, C)],
                areas: [new("new-one", ["src/Drops.cs"]), new(A, ["src/Widget.cs"])]));
        await _h.BindAsync(proposal);

        await _h.AcceptAsync(proposal);

        var contract = (await _h.RecordAsync())!.Set.Contract!;
        contract.Names.Should().ContainSingle("C is dropped by the epic, so its entry names no spec of the series")
            .Which.Should().Match<SeriesContractName>(n => n.Owner == D && n.Writers!.Single() == B);
        contract.Areas.Select(a => a.Spec).Should().Equal(D, A);
    }

    [Fact]
    public async Task TicketContinuationAct_ProposalWithoutContract_KeepsTheBasisContract()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A]);
        var basis = SeriesContractFixtures.Of(names: [new("Widget", "type", null, A, [B])]);
        await _h.Approvals.SaveAsync(new SpecApprovalRecord(ContinuationHarness.Key, new SpecSet(
            ContinuationHarness.Key, [], SpecAccounting.Empty, [], SpecSource.Approved, Series: Series) { Contract = basis },
            [], "gh"), default);
        var proposal = new SpecOutcome(Draft(B, "B, re-cut"));
        await _h.BindAsync(proposal);

        await _h.AcceptAsync(proposal);

        (await _h.RecordAsync())!.Set.Contract.Should().BeSameAs(basis);
    }

    private static SpecDraft Draft(string id, string goal) =>
        new(id, goal, $"spec: {id}\ngoal: \"{goal}\"\ndone:\n  - \"{goal} works.\"\n", []) { Repos = ["api"], Done = [$"{goal} works."] };
}
