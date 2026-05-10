using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Triage;

public sealed class PlanAnswerParserTests
{
    private readonly PlanAnswerParser _parser = new();

    [Fact]
    public void Parse_AnchoredReplyWithAnswers_ReturnsMap()
    {
        var parent = $"{OpenQuestionsCommentMarkers.MarkdownLeadingMarker}\n**Q1:** ?\n**Q2:** ?";
        var reply = "Q1: oauth\nQ2: postgres";

        var answers = _parser.Parse(parent, reply);

        answers.Should().HaveCount(2);
        answers["1"].Should().Be("oauth");
        answers["2"].Should().Be("postgres");
    }

    [Fact]
    public void Parse_NoLeadingMarker_ReturnsEmpty()
    {
        var answers = _parser.Parse("regular comment", "Q1: yes");

        answers.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MalformedAnswer_SkipsLine()
    {
        var parent = OpenQuestionsCommentMarkers.MarkdownLeadingMarker;
        var reply = "Q1:\nQ2: real answer";

        var answers = _parser.Parse(parent, reply);

        answers.Should().HaveCount(1);
        answers.Should().ContainKey("2");
        answers.Should().NotContainKey("1");
    }

    [Fact]
    public void Parse_SingleArgQuoteReply_DetectsViaMarkerInBody()
    {
        var quoteReply =
            $"> {OpenQuestionsCommentMarkers.MarkdownLeadingMarker}\n> **Q1:** ?\n\nQ1: yes";

        var answers = _parser.Parse(quoteReply);

        answers.Should().HaveCount(1);
        answers["1"].Should().Be("yes");
    }

    [Fact]
    public void Parse_PlainTextLeadingMarker_AlsoDetected()
    {
        var quoteReply =
            $"{OpenQuestionsCommentMarkers.PlainTextLeadingMarker}\n[Q1] Q1: do something\n\nQ1: ok";

        var answers = _parser.Parse(quoteReply);

        answers.Should().HaveCount(1);
        answers["1"].Should().Be("ok");
    }
}
