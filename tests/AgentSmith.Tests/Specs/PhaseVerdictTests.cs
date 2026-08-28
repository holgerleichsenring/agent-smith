using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0420: the phase verdict comes from the accounts, and an outstanding criterion names
/// itself. The old gate answered "this run produced no code changes" — true of every
/// resumed branch, and it tells the operator nothing about what is actually missing.
/// </summary>
public sealed class PhaseVerdictTests
{
    private static readonly CommandResult Green = CommandResult.Ok("Verified: sample-repo [build+test] green");

    [Fact]
    public void EveryCriterionAccountedFor_IsADelivery()
    {
        var verdict = PhaseVerdict.From(Green, [Account(
            Satisfied("packages updated", "src/Api/Api.csproj"),
            Satisfied("call sites adapted", "src/Api/Startup.cs"))]);

        verdict.IsSuccess.Should().BeTrue();
        verdict.Message.Should().Contain("all 2 ratified criterion(s) are accounted for");
    }

    [Fact]
    public void ResumedBranchWhoseWorkIsAlreadyDone_IsADelivery()
    {
        // The account reads the BRANCH. Whether this particular run wrote the change is
        // not a question the gate asks any more — run c96d was recorded FAILED for
        // exactly that, with a complete delivery sitting in its pull request.
        var verdict = PhaseVerdict.From(Green, [Account(Satisfied("packages updated", "src/Api/Api.csproj"))]);

        verdict.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AnOutstandingCriterion_FailsAndNamesTheCriterionAndTheRepo()
    {
        var verdict = PhaseVerdict.From(Green, [
            Account(Satisfied("packages updated", "src/Api/Api.csproj")),
            new SpecAccount("worker-repo", [
                new CriterionAccount("packages updated", AccountDisposition.NotSatisfied, Note: "nothing in the diff touches a manifest")])]);

        verdict.IsSuccess.Should().BeFalse();
        verdict.Message.Should().Contain("worker-repo: packages updated");
        verdict.Message.Should().Contain("nothing in the diff touches a manifest");
    }

    [Fact]
    public void APhaseThatCouldNotBeAccountedFor_IsNotADelivery()
    {
        var verdict = PhaseVerdict.From(Green, [
            new SpecAccount("sample-repo", [], "the delivery diff could not be taken (no comparable base)")]);

        verdict.IsSuccess.Should().BeFalse();
        verdict.Message.Should().Contain("An unaccounted phase is not a delivered one");
    }

    [Fact]
    public void NoCriteriaAtAll_LeavesTheMechanicalVerdictAlone()
    {
        // A run without a ratified spec keeps the gate it had — this phase adds a
        // question, it does not invent criteria nobody stated.
        var verdict = PhaseVerdict.From(Green, []);

        verdict.IsSuccess.Should().BeTrue();
        verdict.Message.Should().Be(Green.Message);
    }

    /// <summary>
    /// 2026-08-25-9749: a criterion the base disproves is settled, not open. Handed back as
    /// outstanding it becomes an instruction to BUILD the thing another criterion of the same
    /// phase forbids — a wrong no is not a wasted repair pass, it is the wrong work.
    /// </summary>
    [Fact]
    public void PhaseVerdict_ANotApplicableCriterion_IsNotHandedBackForRepair()
    {
        var accounts = new[]
        {
            Account(
                Satisfied("packages updated", "src/Api/Api.csproj"),
                Declined("the transport is configured on every host that had one")),
        };

        PhaseVerdict.Outstanding(accounts).Should().BeEmpty();
        PhaseVerdict.IsRepairable(accounts).Should().BeFalse();
        PhaseVerdict.From(Green, accounts).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void PhaseVerdict_EveryCriterionNotApplicable_IsNotAPass()
    {
        var verdict = PhaseVerdict.From(Green, [Account(
            Declined("the transport is configured on every host that had one"),
            Declined("every previously shortened subscription keeps its shortening"))]);

        verdict.IsSuccess.Should().BeFalse(
            "not applicable is not a pass — a delivery has to prove something");
    }

    [Fact]
    public void PhaseVerdict_ARefusalWithNothingOutstanding_NamesWhatItDeclinedToJudge()
    {
        var accounts = new[] { Account(Declined("the transport is configured on every host that had one")) };

        var verdict = PhaseVerdict.From(Green, accounts);

        verdict.Message.Should().Contain("the transport is configured on every host that had one");
        verdict.Message.Should().Contain(Antecedent,
            "an operator can only overrule an antecedent the refusal names");
        PhaseVerdict.IsRepairable(accounts).Should().BeFalse(
            "there is no outstanding criterion for a repair pass to close");
    }

    private const string Antecedent = "a previously configured transport";

    private static CriterionAccount Declined(string criterion) =>
        new(criterion, AccountDisposition.NotApplicable,
            "repo@origin/main: the account searched 'Transport' exited 1", Antecedent: Antecedent);

    private static SpecAccount Account(params CriterionAccount[] criteria) =>
        new("sample-repo", criteria);

    private static CriterionAccount Satisfied(string criterion, string citation) =>
        new(criterion, AccountDisposition.Satisfied, citation);
}
