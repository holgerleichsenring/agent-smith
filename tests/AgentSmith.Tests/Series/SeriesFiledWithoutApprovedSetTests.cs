using AgentSmith.Contracts.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-26-5f6e: a ticket its series filed from a text — its check's follow-up, its master's add_ticket — carries
/// no approved set; a ticket a list filed does, and is never counted.</summary>
public sealed class SeriesFiledWithoutApprovedSetTests
{
    [Fact]
    public void FiledWithoutApprovedSet_FollowUpAndMasterAddition_AreCounted_AListTicketIsNot()
    {
        var record = SeriesRecords.Running() with
        {
            Tickets = [SeriesRecords.Ticket("43"), SeriesRecords.Ticket("44") with { FollowUp = true }, SeriesRecords.Ticket("46")],
            Acts =
            [
                new SeriesAct(SeriesRecords.T, "retry_ticket", "43", "retried SCRUM-43", null),
                new SeriesAct(SeriesRecords.T, SeriesAct.AddTicketTool, "46", "added SCRUM-46", "a prerequisite"),
            ],
        };

        new[] { "43", "44", "46" }.Select(record.FiledWithoutApprovedSet).Should().Equal(false, true, true);
    }
}
