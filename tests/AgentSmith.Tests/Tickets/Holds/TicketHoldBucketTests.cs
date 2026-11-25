using AgentSmith.Application.Services.Holds;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Holds;
using AgentSmith.Server.Services.Tickets.Text;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-31dcg: the small reads a hold is gathered by — the restart window, the open text edits, the series' held tickets.</summary>
public sealed class TicketHoldBucketTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-23T09:00:00Z");

    private static TicketHold Hold(string addressee, string @case, DateTimeOffset? since = null) => new(addressee, @case, null, since);

    [Fact]
    public void Inputs_Restarts_OnlyAnEndInsideTheWindow()
    {
        var inputs = TicketHoldInputs.None with { RestartSince = T };

        (inputs.Restarts(T.AddMinutes(1)), inputs.Restarts(T.AddMinutes(-1)), inputs.Restarts(null), TicketHoldInputs.None.Restarts(T))
            .Should().Be((true, false, false, false));
    }

    [Fact]
    public void EditNotice_Open_AnEditWithoutItsAnsweredMark()
    {
        TicketTextEditNotice.Open([
            TicketTextEditNotice.KeyPrefix + "fp-1", TicketTextEditNotice.KeyPrefix + "fp-2", TicketTextEditNotice.AnsweredPrefix + "fp-1", "other|x",
        ]).Should().Equal("fp-2");
    }
}
