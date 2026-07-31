using AgentSmith.Application.Services.WorkSpecs;
using AgentSmith.Contracts.WorkSpecs;
using FluentAssertions;

namespace AgentSmith.Tests.WorkSpecs;

// p0390: the two guards on a master-issued revision. The progress guard is the
// delicate one — it must NOT fire on the case this phase exists for ("the master
// read the code and found the requirement does not fit", written before any
// source edit) and must fire on the one it does not (revising in circles after
// the work started).
public sealed class WorkSpecRevisionGuardTests
{
    private static WorkSpec Spec(bool readOnlyDone, params string[] done) => new(
        "p-1", "goal", ["req"], [], done, readOnlyDone, [],
        [new WorkSpecRevision(1, "initial", DateTimeOffset.UnixEpoch)]);

    [Fact]
    public void ReviseWorkSpec_DoneSectionWithExpectation_IsRejectedAsReadOnly() =>
        WorkSpecRevisionGuards.RefuseDoneEdit(Spec(true, "a", "b"), ["a", "changed"])
            .Should().NotBeNull().And.Subject.ToString().Should().Contain("read-only");

    [Fact]
    public void RefuseDoneEdit_ReadOnlyButUnchangedList_IsAllowed() =>
        WorkSpecRevisionGuards.RefuseDoneEdit(Spec(true, "a", "b"), ["a", "b"])
            .Should().BeNull("echoing the list back is not an edit");

    [Fact]
    public void RefuseDoneEdit_ReadOnlyAndDoneOmitted_IsAllowed() =>
        WorkSpecRevisionGuards.RefuseDoneEdit(Spec(true, "a"), null).Should().BeNull();

    [Fact]
    public void RefuseDoneEdit_NoExpectation_TheSpecsOwnListIsRevisable() =>
        WorkSpecRevisionGuards.RefuseDoneEdit(Spec(false, "a"), ["b"]).Should().BeNull();

    [Fact]
    public void ReviseWorkSpec_SecondRevisionBeforeAnySourceCommit_IsAllowed() =>
        // HEAD is still the spec commit — nothing has been built yet, so a second
        // revision is exactly the intended "I read the code and it does not fit".
        WorkSpecRevisionGuards.RefuseUnproductiveRevision(
            specCommitSha: "spec1", headSha: "spec1", shaAtLastRevision: "spec1")
        .Should().BeNull();

    [Fact]
    public void ReviseWorkSpec_SecondRevisionAfterFirstSourceCommitWithoutNewCommit_IsRefused() =>
        WorkSpecRevisionGuards.RefuseUnproductiveRevision(
            specCommitSha: "spec1", headSha: "src2", shaAtLastRevision: "src2")
        .Should().NotBeNull().And.Subject.ToString().Should().Contain("committed since");

    [Fact]
    public void RefuseUnproductiveRevision_NewSourceCommitSinceTheLastRevision_IsAllowed() =>
        WorkSpecRevisionGuards.RefuseUnproductiveRevision(
            specCommitSha: "spec1", headSha: "src3", shaAtLastRevision: "src2")
        .Should().BeNull();

    [Fact]
    public void RefuseUnproductiveRevision_UnknownHead_DoesNotFire() =>
        // No sandbox / no git answer: degrade to permitting the revision rather
        // than refusing work on a measurement we could not take.
        WorkSpecRevisionGuards.RefuseUnproductiveRevision("spec1", string.Empty, string.Empty)
            .Should().BeNull();
}
