using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services.Adapters;
using FluentAssertions;
using System.Text.Json;

namespace AgentSmith.Tests.Dispatcher;

public sealed class SlackModalBuilderTests
{
    private const string PrivateMetadata = "{\"channel_id\":\"C123\",\"user_id\":\"U456\"}";

    [Fact]
    public void BuildInitialView_ContainsCommandDropdown()
    {
        var view = SlackModalBuilder.BuildInitialView(PrivateMetadata);

        var json = JsonSerializer.Serialize(view);
        json.Should().Contain("command_select");
        json.Should().Contain("command_action");
        json.Should().Contain("Fix Ticket");
        json.Should().Contain("List Tickets");
        json.Should().Contain("Create Ticket");
    }

    [Fact]
    public void BuildInitialView_ContainsProjectExternalSelect()
    {
        var view = SlackModalBuilder.BuildInitialView(PrivateMetadata);

        var json = JsonSerializer.Serialize(view);
        json.Should().Contain("project_select");
        json.Should().Contain("external_select");
        json.Should().Contain("project_action");
    }

    [Fact]
    public void BuildInitialView_IncludesPrivateMetadata()
    {
        var view = SlackModalBuilder.BuildInitialView(PrivateMetadata);

        var json = JsonSerializer.Serialize(view);
        json.Should().Contain("channel_id");
        json.Should().Contain("C123");
    }

    [Fact]
    public void BuildUpdatedView_FixTicket_ShowsTicketField()
    {
        var view = SlackModalBuilder.BuildUpdatedView(
            ModalCommandType.FixTicket, PrivateMetadata, "my-project");

        var json = JsonSerializer.Serialize(view);
        json.Should().Contain("ticket_select");
        json.Should().Contain("ticket_action");
        json.Should().NotContain("ticket_title");
    }

    [Fact]
    public void BuildUpdatedView_CreateTicket_ShowsTitleAndDescription()
    {
        var view = SlackModalBuilder.BuildUpdatedView(
            ModalCommandType.CreateTicket, PrivateMetadata, "my-project");

        var json = JsonSerializer.Serialize(view);
        json.Should().Contain("ticket_title");
        json.Should().Contain("ticket_description");
        json.Should().NotContain("ticket_select");
    }

    [Fact]
    public void BuildUpdatedView_ListTickets_HidesTicketField()
    {
        var view = SlackModalBuilder.BuildUpdatedView(
            ModalCommandType.ListTickets, PrivateMetadata, "my-project");

        var json = JsonSerializer.Serialize(view);
        json.Should().NotContain("ticket_select");
        json.Should().NotContain("ticket_title");
        json.Should().NotContain("ticket_description");
    }

    [Fact]
    public void BuildUpdatedView_FixTicket_WithPipelines_ShowsPipelineDropdown()
    {
        var pipelines = new List<string> { "fix-bug", "fix-no-test", "add-feature" };

        var view = SlackModalBuilder.BuildUpdatedView(
            ModalCommandType.FixTicket, PrivateMetadata, "my-project", pipelines);

        var json = JsonSerializer.Serialize(view);
        json.Should().Contain("pipeline_select");
        json.Should().Contain("fix-bug");
        json.Should().Contain("fix-no-test");
    }

    [Fact]
    public void BuildProjectOptions_FiltersbyQuery()
    {
        var projects = new List<string> { "agent-smith", "agent-smith-test", "todo-app" };

        var result = SlackModalBuilder.BuildProjectOptions(projects, "agent");

        var json = JsonSerializer.Serialize(result);
        json.Should().Contain("agent-smith");
        json.Should().Contain("agent-smith-test");
        json.Should().NotContain("todo-app");
    }

    [Fact]
    public void BuildProjectOptions_EmptyQuery_ReturnsAll()
    {
        var projects = new List<string> { "agent-smith", "todo-app" };

        var result = SlackModalBuilder.BuildProjectOptions(projects, null);

        var json = JsonSerializer.Serialize(result);
        json.Should().Contain("agent-smith");
        json.Should().Contain("todo-app");
    }

    [Fact]
    public void BuildTicketOptions_FormatsCorrectly()
    {
        var tickets = new List<(int Id, string Title)>
        {
            (42, "Fix login bug"),
            (58, "Add dark mode")
        };

        var result = SlackModalBuilder.BuildTicketOptions(tickets, null);

        var json = JsonSerializer.Serialize(result);
        json.Should().Contain("#42");
        json.Should().Contain("Fix login bug");
        json.Should().Contain("58");
    }

    [Fact]
    public void BuildTicketOptions_FiltersbyQuery()
    {
        var tickets = new List<(int Id, string Title)>
        {
            (42, "Fix login bug"),
            (58, "Add dark mode")
        };

        var result = SlackModalBuilder.BuildTicketOptions(tickets, "login");

        var json = JsonSerializer.Serialize(result);
        json.Should().Contain("Fix login bug");
        json.Should().NotContain("dark mode");
    }

    [Theory]
    [InlineData("fix_ticket", ModalCommandType.FixTicket)]
    [InlineData("list_tickets", ModalCommandType.ListTickets)]
    [InlineData("create_ticket", ModalCommandType.CreateTicket)]
    public void ParseCommandValue_ValidValues_ReturnsCorrectType(string value, ModalCommandType expected)
    {
        SlackModalBuilder.ParseCommandValue(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void ParseCommandValue_InvalidValues_ReturnsNull(string? value)
    {
        SlackModalBuilder.ParseCommandValue(value).Should().BeNull();
    }
}
