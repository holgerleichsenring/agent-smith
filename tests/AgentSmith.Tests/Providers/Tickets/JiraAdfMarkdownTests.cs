using System.Text.Json;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// A Jira comment body (ADF) is read as markdown: its structure survives, its text nodes stay verbatim, and
/// what we posted as ADF text reads back with every marker and keyword still matching.
/// </summary>
public sealed class JiraAdfMarkdownTests
{
    private static JsonElement Adf(string content) =>
        JsonDocument.Parse($$"""{ "type": "doc", "version": 1, "content": [{{content}}] }""").RootElement;

    [Fact]
    public void Write_Structure_BecomesMarkdown()
    {
        var doc = Adf("""
            { "type": "paragraph", "content": [{ "type": "text", "text": "Hello" }, { "type": "hardBreak" }, { "type": "text", "text": "world" }] },
            { "type": "heading", "attrs": { "level": 2 }, "content": [{ "type": "text", "text": "Plan" }] },
            { "type": "bulletList", "content": [
              { "type": "listItem", "content": [{ "type": "paragraph", "content": [{ "type": "text", "text": "one" }] }] },
              { "type": "listItem", "content": [{ "type": "paragraph", "content": [{ "type": "text", "text": "two" }] }] }] },
            { "type": "taskList", "content": [
              { "type": "taskItem", "attrs": { "state": "DONE" }, "content": [{ "type": "text", "text": "done" }] },
              { "type": "taskItem", "attrs": { "state": "TODO" }, "content": [{ "type": "text", "text": "todo" }] }] },
            { "type": "paragraph", "content": [
              { "type": "text", "text": "bold", "marks": [{ "type": "strong" }] }, { "type": "text", "text": " and " },
              { "type": "text", "text": "site", "marks": [{ "type": "link", "attrs": { "href": "https://example.com/a" } }] }] }
            """);

        JiraAdfMarkdown.Write(doc).Should().Be(
            "Hello  \nworld\n\n## Plan\n\n- one\n- two\n\n- [x] done\n- [ ] todo\n\n**bold** and [site](https://example.com/a)");
    }

    [Fact]
    public void Write_InlineCard_BecomesALink()
    {
        var doc = Adf("""{ "type": "paragraph", "content": [{ "type": "inlineCard", "attrs": { "url": "https://example.com/b" } }] }""");

        JiraAdfMarkdown.Write(doc).Should().Be("[https://example.com/b](https://example.com/b)");
    }

    [Fact]
    public void Write_TextNodeWithAngleBracket_IsEscapedNotHtml()
    {
        var doc = Adf("""{ "type": "paragraph", "content": [{ "type": "text", "text": "List<string>" }] }""");

        JiraAdfMarkdown.Write(doc).Should().Be(@"List\<string>", "ADF text is never HTML, so a '<' must not open a tag");
    }

    [Fact]
    public void JiraCommentMapper_MarksMarkdown()
    {
        var root = JsonDocument.Parse("""{ "comments": [{ "id": "1", "body": { "type": "doc", "content": [] } }] }""").RootElement;

        new JiraCommentMapper().MapMany(root).Single().Format.Should().Be(TicketTextFormat.Markdown);
    }

    [Fact]
    public void JiraCommentMapper_OurPostedComment_StaysOursAndADemandStillDemands()
    {
        var ours = Mapped(JiraAdfRenderer.CommentBody("<b>[agent-smith open questions]</b><br/>1. Which branch?"));
        var demand = Mapped(JiraAdfRenderer.CommentBody("cut this specification again<br/>the scope moved"));

        OwnTicketComment.IsOurs(ours).Should().BeTrue();
        SpecRecutDemand.Demands(demand).Should().BeTrue();
    }

    private static TicketComment Mapped(object posted)
    {
        var body = JsonDocument.Parse(JsonSerializer.Serialize(posted)).RootElement.GetProperty("body").GetRawText();
        var root = JsonDocument.Parse(
            $$"""{ "comments": [{ "id": "9", "created": "2026-07-01T10:15:00.000+0000", "body": {{body}} }] }""").RootElement;
        return new JiraCommentMapper().MapMany(root).Single();
    }
}
