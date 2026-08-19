using AgentSmith.Domain.Models;
using Microsoft.VisualStudio.Services.WebApi;
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
    public void Map_BugWithReproStepsOnly_UsesReproStepsAsDescription()
    {
        // p0318: a Bug work item leaves System.Description empty and carries its
        // body in Microsoft.VSTS.TCM.ReproSteps — the planner must receive that text.
        var fields = new Dictionary<string, object>
        {
            ["System.Title"] = "Blank page on first load",
            ["Microsoft.VSTS.TCM.ReproSteps"] = "Open /home, applications list is empty until F5",
            ["System.State"] = "Active",
        };

        var ticket = Sut.Map(new TicketId("18969"), fields);

        ticket.Description.Should().Be("Open /home, applications list is empty until F5");
    }

    [Fact]
    public void Map_WithSystemDescription_PrefersSystemDescription()
    {
        // Non-Bug types that legitimately use System.Description must be unchanged:
        // Description wins even when ReproSteps is also present.
        var fields = new Dictionary<string, object>
        {
            ["System.Title"] = "T",
            ["System.Description"] = "the real description",
            ["Microsoft.VSTS.TCM.ReproSteps"] = "repro fallback",
            ["System.State"] = "Active",
        };

        var ticket = Sut.Map(new TicketId("1"), fields);

        ticket.Description.Should().Be("the real description");
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

    // p0454: the work item names an assignee and a creator; without the identity GUID no
    // mention Azure DevOps delivers can be written, so both halves are carried.
    [Fact]
    public void AnAssignedWorkItem_CarriesItsAssigneeAndReporter()
    {
        var fields = new Dictionary<string, object>
        {
            ["System.Title"] = "T",
            ["System.AssignedTo"] = new IdentityRef { DisplayName = "Jane Operator", Id = "guid-1" },
            ["System.CreatedBy"] = new IdentityRef { DisplayName = "Sam Reporter", Id = "guid-2" }
        };

        var ticket = Sut.Map(new TicketId("17"), fields);

        ticket.Assignee.Should().Be(new TicketPerson("Jane Operator", "guid-1"));
        ticket.Reporter.Should().Be(new TicketPerson("Sam Reporter", "guid-2"));
    }

    [Fact]
    public void AnUnassignedWorkItem_NamesNobody()
    {
        var ticket = Sut.Map(new TicketId("17"), new Dictionary<string, object>
        {
            ["System.Title"] = "T",
            ["System.AssignedTo"] = new IdentityRef { DisplayName = "Jane Operator" }
        });

        ticket.Assignee.Should().BeNull("an identity without its GUID cannot be mentioned");
        ticket.Reporter.Should().BeNull();
    }
}
