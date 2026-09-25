using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using FluentAssertions;

namespace AgentSmith.Tests.Triggers;

/// <summary>
/// 2026-09-25-8e51a: which project a TICKET names for its design conversation — the projects
/// alone, with none of the run side's decisions.
/// <para>
/// A conversation may be had about any ticket on the board, including one nothing would route and
/// one whose project is blocked at startup. What it may NOT do is pick a project on another
/// tracker: the project decides which tracker the conversation files into.
/// </para>
/// </summary>
public sealed class TicketProjectMatchTests
{
    [Fact]
    public void DialogScope_ATicketMatchingOneProjectOnItsOwnTracker_ResolvesToIt()
    {
        var matches = TicketProjectMatch.Of(Config(), Envelope("jira", "proj"));

        matches.Matched.Should().BeEquivalentTo(["alpha"]);
        matches.Unanswerable.Should().BeEmpty();
    }

    [Fact]
    public void DialogScope_ATicketMatchingAProjectOnAnotherTracker_DoesNotResolveToIt()
    {
        // The predicate never reads the platform, so this tag matches BOTH projects. The project
        // decides which tracker the conversation will file into, so the ticket's own wins.
        var matches = TicketProjectMatch.Of(Config(), Envelope("github", "proj"));

        matches.Matched.Should().BeEquivalentTo(["gamma"]);
    }

    [Fact]
    public void DialogScope_AProjectRoutedByAreaPath_IsUnanswerableRatherThanUnmatched()
    {
        var matches = TicketProjectMatch.Of(AreaPathConfig(), Envelope("jira", "proj"));

        matches.Matched.Should().BeEmpty();
        matches.Unanswerable.Should().BeEquivalentTo(["beta"],
            "a ticket read by id carries no area path — that is not the operator's configuration "
            + "being wrong, and saying so is the difference between a reason and an accusation");
    }

    [Fact]
    public void DialogScope_ATicketNothingNames_MatchesNothingAndAccusesNobody()
    {
        var matches = TicketProjectMatch.Of(Config(), Envelope("jira", "something-else"));

        matches.Matched.Should().BeEmpty();
        matches.Unanswerable.Should().BeEmpty();
    }

    private static IncomingTicketEnvelope Envelope(string platform, params string[] labels) =>
        new() { TicketId = "1", Platform = platform, Labels = labels };

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = Project("alpha", jira: Tag("proj")),
            ["gamma"] = Project("gamma", github: Tag("proj")),
        },
    };

    private static AgentSmithConfig AreaPathConfig() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["beta"] = Project("beta", jira: new JiraTriggerConfig
            {
                ProjectResolution = new ProjectResolutionConfig
                {
                    Strategy = ResolutionStrategy.AreaPath,
                    Value = @"Contoso\Platform",
                },
            }),
        },
    };

    private static JiraTriggerConfig Tag(string value) => new()
    {
        ProjectResolution = new ProjectResolutionConfig
        {
            Strategy = ResolutionStrategy.Tag,
            Value = value,
        },
    };

    private static ResolvedProject Project(
        string name, JiraTriggerConfig? jira = null, JiraTriggerConfig? github = null) => new()
    {
        Name = name,
        JiraTrigger = jira,
        GithubTrigger = github,
    };
}
