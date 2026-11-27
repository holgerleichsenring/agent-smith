using AgentSmith.Application.Services.Series;
using AgentSmith.Server.Services.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-27-3532a: a ticket's start wake is raised once per series, ticket and latest attempt — a re-poll and the
/// filing's own record wake the master once, a trigger after a new attempt wakes it again and says retry_ticket runs it.</summary>
public sealed class SeriesStartWakesTests
{
    [Fact]
    public void Recorded_TheFilingAndAnUnworkedTrigger_WakeOnce()
    {
        var record = SeriesStartWakes.Recorded(SeriesRecords.Running(), ["43"], SeriesStartWakes.Filed, SeriesRecords.T);
        var entry = SeriesTicketEntry.Wake(record.Id, "43", attempt: null);

        record.Raised.Should().Contain(entry.Once!, "the trigger's key is the filing's");
        SeriesStartWakes.Recorded(record, ["43"], SeriesStartWakes.Filed, SeriesRecords.T).Wakes.Should().HaveCount(1);
    }

    [Fact]
    public void Wake_AfterAnAttempt_IsANewKey_AndSaysRetry()
    {
        var unworked = SeriesTicketEntry.Wake("sr-1", "43", attempt: null);
        var worked = SeriesTicketEntry.Wake("sr-1", "43", attempt: "run-9");

        worked.Once.Should().Be("start:sr-1:43:run-9").And.NotBe(unworked.Once);
        worked.Text.Should().Contain("run run-9").And.Contain("retry_ticket");
        unworked.Text.Should().Contain("start_ticket");
    }
}
