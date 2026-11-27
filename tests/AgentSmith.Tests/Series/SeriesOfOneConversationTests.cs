using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-27-ecf2e (operator): a series of one talks in its ticket's conversation — the ticket page's chat — so its
/// master asks there and the operator answers there; one question, one place.</summary>
public sealed class SeriesOfOneConversationTests
{
    [Fact]
    public async Task Read_ASeriesOfOneWithoutConversation_TakesItsTicketsConversation()
    {
        var rig = new SeriesWakeBeatRig { Facts = [SeriesRecords.Fact("43")] };
        rig.Conversation.Setup(c => c.TicketConversationAsync(It.IsAny<ResolvedProject>(), "43", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ticket-conv");
        var one = SeriesRecords.Running(conversation: "") with { Tickets = [SeriesRecords.Ticket("43")] };

        var beat = await rig.BeatAsync(one, SeriesRecords.T);

        rig.Store.Records["sr-1"].Conversation.Should().Be("ticket-conv");
        beat.Record.Conversation.Should().Be("ticket-conv");
    }

    [Fact]
    public async Task Read_ASeriesOfSeveral_KeepsItsConversation()
    {
        var rig = new SeriesWakeBeatRig();
        var several = SeriesRecords.Running(conversation: "");

        await rig.BeatAsync(several, SeriesRecords.T);

        rig.Store.Records["sr-1"].Conversation.Should().BeEmpty();
        rig.Conversation.Verify(c => c.TicketConversationAsync(It.IsAny<ResolvedProject>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Questions_ASeriesOfOneWithAConversation_Leads()
    {
        var one = SeriesRecords.Running() with { Tickets = [SeriesRecords.Ticket("43")] };

        SeriesLeads.Questions(one, 10m).Should().BeTrue();
        SeriesLeads.Questions(one with { Conversation = "" }, 10m).Should().BeFalse("without one its ticket page keeps the question");
    }
}
