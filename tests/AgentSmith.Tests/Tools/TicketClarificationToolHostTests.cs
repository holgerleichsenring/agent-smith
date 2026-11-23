using AgentSmith.Application.Services.Tools;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

/// <summary>2026-11-23-31dch: the master's question names who can answer it, and one question per answerer is open at a
/// time — a missing answerer is refused once with every value, so one retry posts it.</summary>
public sealed class TicketClarificationToolHostTests
{
    [Fact]
    public void AskHuman_WithoutAnswerer_IsRefusedOnceWithTheThreeValues_AndTheRetryPosts()
    {
        var host = new TicketClarificationToolHost();

        var refused = host.AskHuman("Which contract does the Worker expect?");

        host.HasAsked.Should().BeFalse("a refused question is not posted");
        refused.Should().StartWith("Not posted").And.Contain("'target'").And.Contain("'plan'").And.Contain("'environment'")
            .And.Contain("series conversation").And.Contain("admin");
        host.AskHuman("Which contract does the Worker expect?", answerer: "plan").Should().Contain("STOP");
        host.Asked.Should().ContainSingle().Which.Answerer.Should().Be(QuestionAnswerers.Plan);
    }

    [Fact]
    public void AskHuman_AnUnknownAnswerer_IsRefusedNamingIt()
    {
        var host = new TicketClarificationToolHost();

        host.AskHuman("Proceed?", answerer: "operator").Should().Contain("'operator' is not an answerer").And.Contain("'target'");
        host.AskHuman("Proceed?", answerer: " Environment ").Should().Contain("STOP");
        host.Asked.Single().Answerer.Should().Be(QuestionAnswerers.Environment, "the value is read ignoring case and whitespace");
    }

    [Fact]
    public void AskHuman_TwoAnswerers_AreBothPosted_ASecondToOneAnswererIsRefused()
    {
        var host = new TicketClarificationToolHost();

        host.AskHuman("Which payload does the Worker read?", answerer: "plan");
        host.AskHuman("The sibling repository cannot be cloned — is the token scoped?", answerer: "environment");
        var second = host.AskHuman("And the field name?", answerer: "plan");

        second.Should().Contain("already posted").And.Contain("'plan'");
        host.Asked.Select(q => (q.Id, q.Answerer)).Should().Equal(("1", "plan"), ("2", "environment"));
    }

    [Fact]
    public void AskHuman_TheRecommendation_ReachesTheQuestion()
    {
        var host = new TicketClarificationToolHost();

        host.AskHuman("Which store?", answerer: "target", choices: [new("SQL"), new("Blob")], recommended_index: 1);

        host.Asked.Single().Should().Match<AgentSmith.Domain.Entities.PlanOpenQuestion>(q =>
            q.Question.EndsWith("Recommended: Blob") && q.RecommendedIndex == 1 && q.Answerer == QuestionAnswerers.Target);
    }

    [Fact]
    public void AskHuman_OnADerivation_ASecondQuestion_IsRefused()
    {
        var host = new TicketClarificationToolHost { Derivation = true };

        host.AskHuman("Which payload?", answerer: "plan");
        host.AskHuman("Token scope?", answerer: "environment").Should().Contain("already handed back");

        host.Asked.Should().ContainSingle("a derivation hands back one question");
    }
}
