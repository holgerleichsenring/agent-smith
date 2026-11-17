using AgentSmith.Application.Services.SpecDialog.Writes;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Writes;

/// <summary>2026-11-17-5701a: a ticket's spec is tickets/&lt;key&gt;/&lt;label&gt;.yaml, and tickets/&lt;key&gt;/ its directory.</summary>
public sealed class DraftPathTests
{
    [Fact]
    public void Item_TicketSpecPath_NamesTheLabelAndItsTicket()
    {
        DraftPath.Item(DraftPath.Bare("draft:tickets/order-api/order-state.yaml"))
            .Should().Be(new DraftItem(DraftItemKind.TicketSpec, "order-state", "order-api"));
        DraftPath.Item("tickets/order-api.yaml").Should().Be(new DraftItem(DraftItemKind.Ticket, "order-api"));
    }

    [Fact]
    public void Item_NestedDeeper_IsOutsideTheLayout()
    {
        DraftPath.Item("tickets/order-api/specs/order-state.yaml").Should().BeNull();
        DraftPath.Item("tickets//order-state.yaml").Should().BeNull();
    }

    [Fact]
    public void OfTicketSpec_IsTheFileItemReadsBack() =>
        DraftPath.Item(DraftPath.OfTicketSpec("order-api", "order-state"))
            .Should().Be(new DraftItem(DraftItemKind.TicketSpec, "order-state", "order-api"));

    [Fact]
    public void TicketDirectory_TicketsKeyDirectory_IsADirectoryOfTheDraft()
    {
        DraftPath.TicketDirectory("tickets/order-api/").Should().Be("order-api");
        DraftPath.TicketDirectory("tickets/order-api.yaml").Should().BeNull();
        DraftPath.TicketDirectory("tickets/").Should().BeNull();
        DraftPath.IsDirectory("tickets/order-api").Should().BeTrue();
        DraftPath.Layout.Should().Contain("draft:tickets/<key>/<label>.yaml");
    }
}
