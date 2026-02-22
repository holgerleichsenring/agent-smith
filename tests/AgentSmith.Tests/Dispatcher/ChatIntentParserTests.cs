using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Dispatcher;

public sealed class ChatIntentParserTests
{
    private readonly ChatIntentParser _parser = new(
        NullLoggerFactory.Instance.CreateLogger<ChatIntentParser>());

    [Theory]
    [InlineData("help")]
    [InlineData("?")]
    [InlineData("what can you do")]
    public void Parse_HelpMessage_ReturnsHelpIntent(string text)
    {
        var intent = _parser.Parse(text, "U1", "C1", "slack");

        intent.Should().BeOfType<HelpIntent>();
    }

    [Theory]
    [InlineData("hi")]
    [InlineData("hello")]
    [InlineData("hey")]
    public void Parse_Greeting_ReturnsGreetingIntent(string text)
    {
        var intent = _parser.Parse(text, "U1", "C1", "slack");

        intent.Should().BeOfType<GreetingIntent>();
    }

    [Fact]
    public void Parse_FixWithProject_ReturnsFixTicketIntent()
    {
        var intent = _parser.Parse("fix #42 in my-project", "U1", "C1", "slack");

        intent.Should().BeOfType<FixTicketIntent>();
        var fix = (FixTicketIntent)intent;
        fix.TicketId.Should().Be(42);
        fix.Project.Should().Be("my-project");
    }

    [Fact]
    public void Parse_TicketIdOnly_ReturnsFixWithEmptyProject()
    {
        var intent = _parser.Parse("#123", "U1", "C1", "slack");

        intent.Should().BeOfType<FixTicketIntent>();
        var fix = (FixTicketIntent)intent;
        fix.TicketId.Should().Be(123);
        fix.Project.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ListTickets_ReturnsListIntent()
    {
        var intent = _parser.Parse("list tickets in backend", "U1", "C1", "slack");

        intent.Should().BeOfType<ListTicketsIntent>();
        var list = (ListTicketsIntent)intent;
        list.Project.Should().Be("backend");
    }

    [Fact]
    public void Parse_CreateTicket_ReturnsCreateIntent()
    {
        var intent = _parser.Parse("create 'Fix login bug' in backend", "U1", "C1", "slack");

        intent.Should().BeOfType<CreateTicketIntent>();
        var create = (CreateTicketIntent)intent;
        create.Title.Should().Be("Fix login bug");
        create.Project.Should().Be("backend");
    }

    [Fact]
    public void Parse_UnknownMessage_ReturnsUnknownIntent()
    {
        var intent = _parser.Parse("something random", "U1", "C1", "slack");

        intent.Should().BeOfType<UnknownIntent>();
    }

    [Fact]
    public void Parse_SetsCommonFields()
    {
        var intent = _parser.Parse("help", "U42", "C99", "teams");

        intent.UserId.Should().Be("U42");
        intent.ChannelId.Should().Be("C99");
        intent.Platform.Should().Be("teams");
    }
}
