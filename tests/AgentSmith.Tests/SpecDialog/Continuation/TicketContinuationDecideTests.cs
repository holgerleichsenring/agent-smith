using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: the one decision the approval preview and the act share — a worked ticket's series
/// continued by spec id, never by position.
/// </summary>
public sealed class TicketContinuationDecideTests
{
    private const string Series = "2026-10-13-ab12";
    private static readonly string A = Series + "a", B = Series + "b", C = Series + "c";

    [Fact]
    public void Decide_ExecutedId_KeptAndChangedTextReportedDiscarded()
    {
        var state = Worked(executed: [A], Basis(A), Basis(B));

        var decision = TicketContinuation.Decide(state, Lone(Draft(A, "spec: a\ngoal: rewritten after it ran")));

        decision.Effect.Should().Be(TicketContinuationEffects.Continue);
        decision.Specs.Single(s => s.SpecId == A).Should().Match<TicketContinuationSpec>(s =>
            s.Disposition == SpecDispositionKinds.Kept && s.Note!.Contains("discarded"));
        decision.Drafts.Should().NotContain(d => d.SpecId == A, "what ran is never recorded as something to apply");
    }

    [Fact]
    public void Decide_RepeatedUnstartedId_IsRecut_NotRepeatedInLoneSpec_IsKept()
    {
        var state = Worked(executed: [A], Basis(A), Basis(B), Basis(C));

        var decision = TicketContinuation.Decide(state, Lone(Draft(B, "spec: b\ngoal: a better b")));

        Kinds(decision).Should().Equal(
            (B, SpecDispositionKinds.Recut), (A, SpecDispositionKinds.Kept), (C, SpecDispositionKinds.Kept));
        TicketContinuation.Decide(state, Lone(Draft(B, Yaml(B)))).Specs.Single(s => s.SpecId == B)
            .Disposition.Should().Be(SpecDispositionKinds.Kept, "a byte-identical repeat changes nothing");
    }

    [Fact]
    public void Decide_UnknownId_AppendedPastEveryTakenId_RequiresRewritten()
    {
        var record = new SpecApprovalRecord("github-42",
            new SpecSet("github-42", [new DerivedSpec(Draft(Series + "d", Yaml(Series + "d")), "d", "", [])],
                SpecAccounting.Empty, [], SpecSource.Approved), []);
        var state = Worked(executed: [A], Basis(A), Basis(B), Basis(C)) with { Record = record };
        var epic = new EpicOutcome(Draft("p-parent", "spec: p-parent"), [
            Draft(B, Yaml(B)), Draft(C, Yaml(C)),
            Draft("new-one", "spec: new-one\nrequires:\n  - " + C + "\n", [C]),
            Draft("new-two", "spec: new-two\nrequires:\n  - new-one\n", ["new-one"])]);

        var decision = TicketContinuation.Decide(state, epic);

        decision.Specs.Where(s => s.Disposition == SpecDispositionKinds.Appended).Select(s => s.SpecId)
            .Should().Equal(Series + "e", Series + "f");
        var two = decision.Drafts.Single(d => d.SpecId == Series + "f");
        two.Requires.Should().Equal(Series + "e");
        two.Yaml.Should().Contain($"spec: {Series}f").And.Contain($"- {Series}e");
    }

    [Fact]
    public void Decide_EpicOmitsUnstarted_Dropped()
    {
        var state = Worked(executed: [A], Basis(A), Basis(B), Basis(C));

        var decision = TicketContinuation.Decide(state,
            new EpicOutcome(Draft("p-parent", "spec: p-parent"), [Draft(B, "spec: b\ngoal: changed")]));

        Kinds(decision).Should().Equal(
            (B, SpecDispositionKinds.Recut), (A, SpecDispositionKinds.Kept), (C, SpecDispositionKinds.Dropped));
        decision.Unstarted.Should().Be(1);
    }

    [Fact]
    public void Decide_PersonClosedTicket_FollowUp_EvenWhenNeverWorked()
    {
        var state = Worked(executed: []) with { ClosedForWork = true, BasisRunId = null };

        var decision = TicketContinuation.Decide(state, Lone(Draft("x", "spec: x")));

        decision.Effect.Should().Be(TicketContinuationEffects.FollowUpClosed);
        decision.Drafts.Should().ContainSingle();
        decision.Reason.Should().Contain("closed by a person");
    }

    private static IEnumerable<(string, string)> Kinds(TicketContinuationDecision decision) =>
        decision.Specs.Select(s => (s.SpecId, s.Disposition));

    private static TicketContinuationState Worked(IReadOnlyList<string> executed, params BasisSpec[] basis) =>
        new("42", false, "r-2", Series, basis, executed, new ContinuationBasis(["api"], "api"), null);

    private static BasisSpec Basis(string id) => new(id, $"Goal {id}", Yaml(id));

    private static string Yaml(string id) => $"spec: {id}\ngoal: Goal {id}\n";

    private static SpecOutcome Lone(SpecDraft draft) => new(draft);

    private static SpecDraft Draft(string id, string yaml, IReadOnlyList<string>? requires = null) =>
        new(id, $"Goal {id}", yaml, requires ?? []);
}
