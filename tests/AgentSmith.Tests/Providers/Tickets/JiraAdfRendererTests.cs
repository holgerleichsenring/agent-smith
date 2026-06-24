using System.Text.Json;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

public sealed class JiraAdfRendererTests
{
    // The status/error comment agent-smith authors for the GitHub/ADO providers —
    // HTML bold + line breaks + an HTML-encoded apostrophe and angle brackets.
    private const string HtmlComment =
        "<b>Agent Smith — Failed</b><br/><b>Step:</b> Creating pull request (16/17)<br/>"
        + "<b>Error:</b> the agent&#39;s output &lt;empty&gt;";

    [Fact]
    public void CommentBody_ConvertsHtmlSubsetToAdf_NoLiteralTagsOrEntities()
    {
        var json = JsonSerializer.Serialize(JiraAdfRenderer.CommentBody(HtmlComment));

        using var doc = JsonDocument.Parse(json);
        var inline = doc.RootElement
            .GetProperty("body").GetProperty("content")[0].GetProperty("content");

        var texts = new List<string>();
        var types = new List<string>();
        var strongTexts = new List<string>();
        foreach (var node in inline.EnumerateArray())
        {
            var type = node.GetProperty("type").GetString()!;
            types.Add(type);
            if (type != "text") continue;
            var text = node.GetProperty("text").GetString()!;
            texts.Add(text);
            if (node.TryGetProperty("marks", out var marks)
                && marks.EnumerateArray().Any(m => m.GetProperty("type").GetString() == "strong"))
                strongTexts.Add(text);
        }

        // No literal HTML tags or entities leak into the rendered text.
        var allText = string.Concat(texts);
        allText.Should().NotContain("<b>").And.NotContain("<br").And.NotContain("&#39;").And.NotContain("&lt;");

        // Bold segments became strong-marked text nodes.
        strongTexts.Should().Contain("Agent Smith — Failed");
        strongTexts.Should().Contain("Step:");
        strongTexts.Should().Contain("Error:");

        // <br/> became hard breaks.
        types.Should().Contain("hardBreak");

        // Entities decoded.
        allText.Should().Contain("the agent's output <empty>");
    }

    [Fact]
    public void FromPlainText_EmptyBody_SubstitutesSpace_AdfRejectsEmptyTextNode()
    {
        var json = JsonSerializer.Serialize(JiraAdfRenderer.FromPlainText(""));
        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("content")[0].GetProperty("content")[0]
            .GetProperty("text").GetString();
        text.Should().Be(" ");
    }
}
