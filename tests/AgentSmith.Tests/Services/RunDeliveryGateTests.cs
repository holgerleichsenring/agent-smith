using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0421: the ONE gate that decides whether a run delivered, reading what each phase
/// accounted for against the branch.
/// <para>
/// The four suites this replaces tested the old gate's six signals. What they were
/// really protecting — a repository in scope that was never worked, edits that never
/// reached the tree, a truncated run reporting itself green — is protected here too,
/// and by a mechanism that cannot be true of a resumed branch by accident.
/// </para>
/// </summary>
public sealed class RunDeliveryGateTests
{
    [Fact]
    public void EveryCriterionSatisfiedAcrossEveryPhase_IsADelivery()
    {
        var verdict = RunDeliveryGate.Evaluate(RunAccounts.Empty
            .With("p1", [Account("api", Met("packages pinned", "src/Api/Api.csproj"))])
            .With("p2", [Account("worker", Met("packages pinned", "src/Worker/Worker.csproj"))]),
            ratifiedCriteria: 2);

        verdict.Satisfied.Should().BeTrue();
    }

    /// <summary>
    /// The p0384 protection, carried over: an in-scope repository that was never worked.
    /// It used to need the scope classifier's expected-change list and a per-repo staged
    /// map; now the repository's own account is simply outstanding.
    /// </summary>
    [Fact]
    public void ARepositoryWhoseCriteriaAreUnsatisfied_FailsTheRun_AndIsNamed()
    {
        var verdict = RunDeliveryGate.Evaluate(RunAccounts.Empty.With("p1", [
            Account("api", Met("packages pinned", "src/Api/Api.csproj")),
            Account("worker", new CriterionAccount(
                "packages pinned", false, Note: "nothing in the diff touches a manifest"))]), ratifiedCriteria: 2);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("worker: packages pinned");
        verdict.FailureReason.Should().Contain("nothing in the diff touches a manifest");
    }

    /// <summary>
    /// The p0244 protection: edits the agent recorded that never reached the tree. They
    /// are not in the diff, so no criterion can cite them — which is the same answer,
    /// arrived at without a second signal to cross-check against.
    /// </summary>
    [Fact]
    public void EditsThatNeverReachedTheTree_CannotSatisfyACriterion()
    {
        var verdict = RunDeliveryGate.Evaluate(RunAccounts.Empty.With("p1", [
            Account("api", new CriterionAccount("packages pinned", false,
                Note: "claimed satisfied by 'src/Api/Api.csproj', which the diff does not touch"))]), ratifiedCriteria: 2);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("which the diff does not touch");
    }

    /// <summary>The p0341c protection: a run that stopped early cannot satisfy what it never reached.</summary>
    [Fact]
    public void APhaseThatNeverRan_LeavesItsCriteriaOutstanding()
    {
        var verdict = RunDeliveryGate.Evaluate(RunAccounts.Empty
            .With("p1", [Account("api", Met("packages pinned", "src/Api/Api.csproj"))])
            .With("p2", [Account("api", new CriterionAccount("call sites adapted", false))]),
            ratifiedCriteria: 2);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("p2 / api: call sites adapted");
    }

    [Fact]
    public void APhaseThatCouldNotBeAccountedFor_IsNotADelivery()
    {
        var verdict = RunDeliveryGate.Evaluate(RunAccounts.Empty.With("p1", [
            new SpecAccount("api", [], "the delivery diff could not be taken (no comparable base)")]), ratifiedCriteria: 2);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("An unaccounted run is not a delivered one");
    }

    /// <summary>
    /// A pipeline that ratified nothing is not judged here. Inventing a requirement
    /// nobody stated is how the old gate came to fail runs that had delivered — and it
    /// is why mad, legal and security needed an exemption list at all.
    /// </summary>
    [Fact]
    public void ARunWithNoRatifiedCriteria_IsNotJudgedHere()
    {
        RunDeliveryGate.Evaluate(RunAccounts.Empty, ratifiedCriteria: 0).Satisfied.Should().BeTrue();
    }

    /// <summary>
    /// Criteria that were ratified and never accounted for are a GAP: silence means no
    /// phase ever measured itself, which is the hollow success the gate exists for.
    /// </summary>
    [Fact]
    public void RatifiedCriteriaWithNoAccountAtAll_FailsTheRun()
    {
        var verdict = RunDeliveryGate.Evaluate(RunAccounts.Empty, ratifiedCriteria: 3);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("accounted for none of them");
    }

    [Fact]
    public void RerunningAPhase_ReplacesItsAccount_RatherThanCountingItTwice()
    {
        var accounts = RunAccounts.Empty
            .With("p1", [Account("api", new CriterionAccount("packages pinned", false))])
            .With("p1", [Account("api", Met("packages pinned", "src/Api/Api.csproj"))]);

        accounts.All.Should().ContainSingle();
        RunDeliveryGate.Evaluate(accounts, ratifiedCriteria: 2).Satisfied.Should().BeTrue();
    }

    /// <summary>
    /// p0421, found in run 8a1f: a run whose phases were ALL already executed on the branch
    /// runs no phase — so nothing accounted for anything, and the gate failed exactly the
    /// case the accounting exists for. The account is taken for the branch regardless of
    /// whether a phase ran now; this test states the shape the gate must then see.
    /// </summary>
    [Fact]
    public void ARunWhoseWorkWasAlreadyOnTheBranch_IsADelivery_WhenTheBranchIsAccountedFor()
    {
        var verdict = RunDeliveryGate.Evaluate(
            RunAccounts.Empty.With("run", [Account("api", Met("packages pinned", "src/Api/Api.csproj"))]),
            ratifiedCriteria: 1);

        verdict.Satisfied.Should().BeTrue(
            "delivery is a property of the branch, not of whether a phase ran in this run");
    }

    private static SpecAccount Account(string repo, params CriterionAccount[] criteria) =>
        new(repo, criteria);

    private static CriterionAccount Met(string criterion, string citation) =>
        new(criterion, true, citation);
}
