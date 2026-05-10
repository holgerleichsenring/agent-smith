using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Services.Providers.Tickets.OpenQuestions;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets;

public sealed class MarkdownOpenQuestionsCommentTemplateTests
{
    [Fact]
    public void Render_TwoQuestions_ProducesAnchoredMarkdown()
    {
        var template = new GitHubOpenQuestionsCommentTemplate();
        var body = template.Render(new[]
        {
            new PlanOpenQuestion("1", "Which framework?", new[] { "node", "go" }),
            new PlanOpenQuestion("2", "Authentication?", new[] { "oauth", "saml" })
        });

        body.Should().Contain(OpenQuestionsCommentMarkers.MarkdownLeadingMarker);
        body.Should().Contain("<!--Q1-->");
        body.Should().Contain("<!--Q2-->");
        body.Should().Contain("**Q1:** Which framework?");
        body.Should().Contain("Options: node, go");
        body.Should().Contain("**Q2:** Authentication?");
    }

    [Fact]
    public void Render_NoOptions_OmitsOptionsLine()
    {
        var template = new GitLabOpenQuestionsCommentTemplate();
        var body = template.Render(new[]
        {
            new PlanOpenQuestion("a", "Open ended?", Array.Empty<string>())
        });

        body.Should().Contain("**Qa:** Open ended?");
        body.Should().NotContain("Options:");
    }

    [Fact]
    public void Render_LeadingMarkerPresent()
    {
        var template = new AzureDevOpsOpenQuestionsCommentTemplate();
        var body = template.Render(new[]
        {
            new PlanOpenQuestion("1", "Anything?", Array.Empty<string>())
        });

        OpenQuestionsCommentMarkers.IsOpenQuestionsComment(body).Should().BeTrue();
    }
}
