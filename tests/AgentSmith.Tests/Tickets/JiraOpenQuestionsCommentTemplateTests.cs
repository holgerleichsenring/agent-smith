using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Services.Providers.Tickets.OpenQuestions;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets;

public sealed class JiraOpenQuestionsCommentTemplateTests
{
    [Fact]
    public void Render_PlainTextLeadingMarker_IsDetected()
    {
        var template = new JiraOpenQuestionsCommentTemplate();
        var body = template.Render(new[]
        {
            new PlanOpenQuestion("1", "Which framework?", new[] { "node", "go" })
        });

        body.Should().Contain(OpenQuestionsCommentMarkers.PlainTextLeadingMarker);
        body.Should().Contain("[Q1]");
        body.Should().Contain("Q1: Which framework?");
        OpenQuestionsCommentMarkers.IsOpenQuestionsComment(body).Should().BeTrue();
    }

    [Fact]
    public void Render_NoHtmlComments_BecauseAdfDoesNotPreserveThem()
    {
        var template = new JiraOpenQuestionsCommentTemplate();
        var body = template.Render(new[]
        {
            new PlanOpenQuestion("1", "Q?", Array.Empty<string>())
        });

        body.Should().NotContain("<!--");
    }
}
