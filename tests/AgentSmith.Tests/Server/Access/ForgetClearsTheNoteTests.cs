using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Server.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-09-14-2b7c: removal cleared the row and the grant and left the in-memory note that
/// decides the person's RETURN, so they were absent from the surface and silently dropped on
/// every request for up to five minutes — through any number of sign-ins, because the
/// suppression is keyed by subject and lives in this process.
/// </summary>
public sealed class ForgetClearsTheNoteTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Buffer_ForgottenSubject_IsObservedAgainOnTheVeryNextRequest()
    {
        var buffer = new CallerObservationBuffer(TimeProvider.System);
        buffer.Observe(Caller("ada"));
        buffer.Drain();

        buffer.Forget("ada");
        buffer.Observe(Caller("ada"));

        buffer.Drain().Should().ContainSingle().Which.Subject.Should().Be("ada");
    }

    [Fact]
    public void Buffer_ForgottenSubject_IsNotFlushedBackFromThePendingBatch()
    {
        // The second way back: an observation taken shortly before the click is still pending,
        // and the repository's upsert ADDS a row that is not there — so a drain landing after
        // the delete re-inserts the person as an observation from before they were forgotten.
        var buffer = new CallerObservationBuffer(TimeProvider.System);
        buffer.Observe(Caller("ada"));

        buffer.Forget("ada");

        buffer.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Buffer_ForgettingOnePerson_LeavesTheOthersSuppressed()
    {
        var buffer = new CallerObservationBuffer(TimeProvider.System);
        buffer.Observe(Caller("ada"));
        buffer.Observe(Caller("bob"));
        buffer.Drain();

        buffer.Forget("ada");
        buffer.Observe(Caller("ada"));
        buffer.Observe(Caller("bob"));

        buffer.Drain().Select(c => c.Subject).Should().BeEquivalentTo(["ada"],
            "the window is what keeps a write off the authorization path, and forgetting one "
            + "person is not a reason to drop it for everybody");
    }

    [Fact]
    public async Task Buffer_Observing_NeverWaitsOnTheGate()
    {
        // The property the whole buffer exists for: noting a caller costs a dictionary write
        // and returns. If Observe ever took the gate, a flush mid-write would stall the
        // authorization path — which is the cost this design was built to avoid.
        var buffer = new CallerObservationBuffer(TimeProvider.System);
        using var held = await buffer.HoldAsync(default);

        buffer.Observe(Caller("ada"));

        buffer.Drain().Should().ContainSingle();
    }

    [Fact]
    public async Task Remove_WhileAFlushHoldsTheGate_WaitsForTheWriteToLandAndThenRemovesIt()
    {
        // Ordering alone closes nothing: a drained batch lives in a list the removal cannot
        // reach. Held against each other, the write lands first and the delete takes it away.
        using var h = new AccessTestHarness();
        h.Buffer.Observe(Caller("ada"));
        var held = await h.Buffer.HoldAsync(default);

        var removal = h.Remover.RemoveAsync("ada", new ChangeAttribution("tester"), default);
        await Task.Delay(50);
        removal.IsCompleted.Should().BeFalse("the removal waits for the batch in flight");

        await h.Observed.UpsertAsync([Caller("ada")], default);
        held.Dispose();

        await removal;
        (await h.Observed.AllAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_APersonWhoKeepsCalling_IsObservedOnTheNextRequest()
    {
        using var h = new AccessTestHarness();
        h.Buffer.Observe(Caller("ada"));
        await h.FlushAsync();

        await h.Remover.RemoveAsync("ada", new ChangeAttribution("tester"), default);
        h.Buffer.Observe(Caller("ada"));
        await h.FlushAsync();

        (await h.Observed.AllAsync(default)).Should().ContainSingle()
            .Which.Subject.Should().Be("ada", "somebody still using the installation belongs back on the surface");
    }

    [Fact]
    public async Task Remove_APersonNotedMomentsEarlier_DoesNotReturnOnTheNextFlush()
    {
        using var h = new AccessTestHarness();
        h.Buffer.Observe(Caller("ada"));

        await h.Remover.RemoveAsync("ada", new ChangeAttribution("tester"), default);
        await h.FlushAsync();

        (await h.Observed.AllAsync(default)).Should().BeEmpty(
            "a removal that the next drain undoes half a minute later is not a removal");
    }

    private static ObservedCaller Caller(string subject) =>
        new(subject, "sub", subject, [], [], false, Now, Now);
}
