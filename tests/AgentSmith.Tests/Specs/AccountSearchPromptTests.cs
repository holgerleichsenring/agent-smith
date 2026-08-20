using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0482: what the account is told when it can look for itself, and what it is told when it
/// cannot. Both wordings have to stand on their own — a run without a sandbox still takes an
/// account, and it must not be instructed to call a tool it was not given.
/// </summary>
public sealed class AccountSearchPromptTests
{
    [Fact]
    public void SpecAccountPrompt_WithSearchableRepositories_TellsTheAccountToSearchForItself()
    {
        var prompt = SpecAccountPrompt.For(["no MassTransit remains"], string.Empty, [], ["api", "worker"]);

        prompt.Should().Contain("search_branch").And.Contain("settle YOURSELF")
            .And.Contain("api, worker", "the account can only name a repository it is told about");
    }

    /// <summary>An absence criterion is now expected to be SEARCHED before it is refused, or
    /// the account trades one false negative for another.</summary>
    [Fact]
    public void SpecAccountPrompt_WithSearchableRepositories_ForbidsRefusingWithoutLooking()
    {
        var prompt = SpecAccountPrompt.For(["no MassTransit remains"], string.Empty, [], ["api"]);

        prompt.Should().Contain("not report one as unsatisfied without having searched");
    }

    [Fact]
    public void SpecAccountPrompt_WithoutASandbox_KeepsTheCitedEvidenceWording()
    {
        var prompt = SpecAccountPrompt.For(["no MassTransit remains"], string.Empty, []);

        prompt.Should().NotContain("search_branch");
        prompt.Should().Contain("answered by the commands listed",
            "an account with no sandbox falls back to what every account did before");
    }

    /// <summary>The rules p0469 and p0481 put in this prompt are load-bearing and this phase
    /// only adds to them.</summary>
    [Fact]
    public void SpecAccountPrompt_StillCarriesTheRulesItsPredecessorsPutThere()
    {
        var prompt = SpecAccountPrompt.For(["criterion"], string.Empty, [], ["api"]);

        prompt.Should().Contain("VERBATIM").And.Contain("SHORTENED")
            .And.Contain("the DIFF wins").And.Contain("could not run");
    }
}
