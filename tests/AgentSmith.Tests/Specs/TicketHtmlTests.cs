using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0399: byte fidelity is owed to the ticket's CONTENT, not to its transport
/// encoding. An Azure DevOps body arrives as HTML — segments cut from it must carry
/// runnable text, while a plain markdown ticket passes through the cut unchanged.
/// </summary>
public sealed class TicketHtmlTests
{
    private const string HtmlTicket =
        "<div>Migrate every handler to the new mediator.</div>"
        + "<div><br></div>"
        + "<div>Find the call sites first:</div>"
        + "<pre>grep -rn &quot;using MediatR&quot; src/ --include=&quot;*.cs&quot;</pre>"
        + "<div>Rename <b>&lt;Domain&gt;Client</b> to &lt;Domain&gt;ServiceClient.</div>";

    private const string MarkdownTicket = """
        Rename `<Domain>Client` to `<Domain>ServiceClient` everywhere.

        ```bash
        grep -rn "using MediatR" src/ && echo done
        ```

        Ping me if anything is unclear.
        """;

    [Fact]
    public void TicketSegments_HtmlTicket_DecodedToPlainCommands()
    {
        var segments = TicketSegmenter.Segment(HtmlTicket);

        var text = string.Join("\n\n", segments.Select(s => s.Text));
        text.Should().NotContain("&quot;").And.NotContain("&lt;").And.NotContain("<div>");
        text.Should().Contain("grep -rn \"using MediatR\" src/ --include=\"*.cs\"");
        text.Should().Contain("Rename <Domain>Client to <Domain>ServiceClient.");
        // The pre block became ONE fenced segment, so the companion renders it runnable.
        segments.Should().Contain(s =>
            s.Text.StartsWith("```") && s.Text.EndsWith("```") && s.Text.Contains("grep -rn"));
        SegmentExtractor.BuildWholeTicketMarkdown("p1", "goal", segments)
            .Should().NotContain("&quot;").And.NotContain("&lt;");
    }

    [Fact]
    public void TicketSegments_MarkdownTicket_Unchanged()
    {
        var segments = TicketSegmenter.Segment(MarkdownTicket);

        segments.Should().HaveCount(3);
        segments[0].Text.Should().Be(
            "Rename `<Domain>Client` to `<Domain>ServiceClient` everywhere.");
        segments[1].Text.Should().Be(
            "```bash\ngrep -rn \"using MediatR\" src/ && echo done\n```");
        segments[2].Text.Should().Be("Ping me if anything is unclear.");
    }

    [Fact]
    public void TicketHtml_EntityEncodedBodyWithoutTags_IsDecoded()
    {
        TicketHtmlConverter.ToText("Run &quot;dotnet test&quot; &amp;&amp; push.")
            .Should().Be("Run \"dotnet test\" && push.");
    }

    [Fact]
    public void TicketHtml_PlainTextBody_PassesThroughByteForByte()
    {
        const string body = "Use List<string> for x < y && y > z.\n\nNothing HTML here.";

        TicketHtmlConverter.ToText(body).Should().Be(body);
    }
}
