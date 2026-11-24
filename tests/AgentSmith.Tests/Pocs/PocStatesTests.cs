using AgentSmith.Contracts.Pocs;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933da: the one definition of a live PoC — a blocked one counts toward the per-person limit until
/// c81ee's removal sets it deleted.</summary>
public sealed class PocStatesTests
{
    [Fact]
    public void IsLive_EveryStateButRefusedAndDeleted()
    {
        PocStates.All.Where(PocStates.IsLive).Should().Equal(
            PocStates.Importing, PocStates.Blocked, PocStates.Ready, PocStates.Failed, PocStates.Stopped);
        PocStates.IsLive(PocStates.Refused).Should().BeFalse();
        PocStates.IsLive(PocStates.Deleted).Should().BeFalse();
    }

    [Fact]
    public void PocActs_All_NamesTheNineActs() =>
        PocActs.All.Should().HaveCount(9).And.OnlyHaveUniqueItems().And.Contain(PocActs.RolledBack);
}
