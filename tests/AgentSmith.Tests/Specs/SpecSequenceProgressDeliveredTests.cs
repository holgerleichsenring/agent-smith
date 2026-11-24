using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-11-23-df05: a run delivered no spec when no row is done over a branch that carried work.</summary>
public sealed class SpecSequenceProgressDeliveredTests
{
    private static SpecSequenceProgress Two() => new([
        new SpecProgress("a", "Goal a", SpecRunState.NotStarted), new SpecProgress("b", "Goal b", SpecRunState.NotStarted)]);

    [Fact]
    public void DeliveredNone_DoneRowsWithoutWork_IsTrue() =>
        Two().With("a", SpecRunState.Done).With("b", SpecRunState.Failed, "dotnet build exited 1")
            .DeliveredNone.Should().BeTrue();

    [Fact]
    public void DeliveredNone_OneDoneRowCarriedWork_IsFalse() =>
        Two().With("a", SpecRunState.Done, carriedWork: true).With("b", SpecRunState.Done)
            .DeliveredNone.Should().BeFalse();

    [Fact]
    public void DeliveredNone_WorkOnARowNotDone_DoesNotCount() =>
        Two().With("a", SpecRunState.InProgress, carriedWork: true).DeliveredNone.Should().BeTrue();

    [Fact]
    public void With_KeepsTheFlagPerRow()
    {
        var progress = Two().With("a", SpecRunState.Done, carriedWork: true).With("b", SpecRunState.Done);

        progress.Specs.Select(p => p.CarriedWork).Should().Equal(true, false);
    }
}
