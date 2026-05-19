using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// p0147f: AzureDevOpsFieldMapper unit tests against the
/// IDictionary&lt;string,object&gt; shape returned by
/// WorkItemTrackingHttpClient.GetWorkItemsAsync.
/// </summary>
public sealed class AzureDevOpsFieldMapperTests
{
    private static readonly AzureDevOpsFieldMapper Sut = new();

    [Fact]
    public void Map_PopulatesAllFieldsFromWorkItemDictionary()
    {
        var fields = new Dictionary<string, object>
        {
            ["System.Title"] = "T",
            ["System.Description"] = "D",
            ["Microsoft.VSTS.Common.AcceptanceCriteria"] = "AC",
            ["System.State"] = "Active",
            ["System.Tags"] = "bug; agent-smith:pending"
        };

        var ticket = Sut.Map(new TicketId("17"), fields);

        ticket.Title.Should().Be("T");
        ticket.Description.Should().Be("D");
        ticket.AcceptanceCriteria.Should().Be("AC");
        ticket.Status.Should().Be("Active");
        ticket.Source.Should().Be("AzureDevOps");
        ticket.Labels.Should().BeEquivalentTo(["bug", "agent-smith:pending"]);
    }

    [Fact]
    public void Map_MissingTags_GivesEmptyLabels()
    {
        var fields = new Dictionary<string, object>
        {
            ["System.Title"] = "T",
            ["System.State"] = "Active"
        };

        var ticket = Sut.Map(new TicketId("1"), fields);

        ticket.Labels.Should().BeEmpty();
        ticket.Description.Should().Be("");
        ticket.AcceptanceCriteria.Should().BeNull();
    }

    [Fact]
    public void Map_TagsAreSplitOnSemicolonAndTrimmed()
    {
        var fields = new Dictionary<string, object>
        {
            ["System.Tags"] = "  red ;  blue  ; ; green "
        };

        var ticket = Sut.Map(new TicketId("1"), fields);

        ticket.Labels.Should().BeEquivalentTo(["red", "blue", "green"]);
    }
}
