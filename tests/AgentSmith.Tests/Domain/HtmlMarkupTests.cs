using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Domain;

public sealed class HtmlMarkupTests
{
    [Theory]
    [InlineData("<p>Export the report.</p>", true)]
    [InlineData("Line<br/>break", true)]
    [InlineData("Title\n=====\n\n*   **Export** the `report` with List<string>.", false)]
    [InlineData("Tom &amp; Jerry", false)]
    [InlineData(null, false)]
    public void IsPresent_AnswersWhetherARecognisedTagIsWritten(string? body, bool expected) =>
        HtmlMarkup.IsPresent(body).Should().Be(expected);

    [Fact]
    public void IsPresent_ATagNameInsideBackticks_StillCountsAsMarkup() =>
        HtmlMarkup.IsPresent("Use a `<div>` here.").Should().BeTrue();

    [Theory]
    [InlineData("<h2>Goal</h2>", TicketTextFormat.Html)]
    [InlineData("## Goal\n\n- export", TicketTextFormat.Markdown)]
    [InlineData("Tom &amp; Jerry", TicketTextFormat.Markdown)]
    [InlineData(null, TicketTextFormat.Markdown)]
    public void OfHtmlField_IsHtmlOnlyWhenTheValueCarriesMarkup(string? value, string expected) =>
        TicketTextFormat.OfHtmlField(value).Should().Be(expected);
}
