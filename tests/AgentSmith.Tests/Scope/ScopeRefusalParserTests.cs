using AgentSmith.Application.Services.Scope;
using FluentAssertions;

namespace AgentSmith.Tests.Scope;

/// <summary>
/// 2026-09-07-a1c3: the refusal is read off the raw scope reply, independently of
/// the repos-array contract — a refusing reply for a single-repository run has no
/// reason to list repos, and a reply without the field is no refusal at all.
/// </summary>
public sealed class ScopeRefusalParserTests
{
    [Fact]
    public void Refusal_AReplyWithoutTheField_ScopesAsToday()
    {
        var reply = """
            {"repos": [{"name": "server", "affected": true, "confidence": 0.9}],
             "complexity": "small", "rationale": "server only"}
            """;

        ScopeRefusalParser.Parse(reply).Should().BeNull();
        RepoScopeParser.TryParse(reply)!.Repos.Should().ContainSingle();
    }

    [Fact]
    public void Parse_AnExplicitNull_IsNoRefusal() =>
        ScopeRefusalParser.Parse("""{"repos": [], "refusal": null}""").Should().BeNull();

    [Fact]
    public void Parse_ARefusingReplyWithoutRepos_StillReadsTheRefusal()
    {
        var refusal = ScopeRefusalParser.Parse("""
            {"complexity": "small",
             "refusal": {"quote": "wipe the production database", "reason": "destroys customer data"}}
            """);

        refusal.Should().NotBeNull();
        refusal!.Quote.Should().Be("wipe the production database");
        refusal.Reason.Should().Be("destroys customer data");
    }

    [Fact]
    public void Parse_ARefusalBesideARepoVerdict_ReadsBoth()
    {
        var reply = """
            Here is my verdict:
            {"repos": [{"name": "server", "affected": true, "confidence": 0.9}],
             "refusal": {"quote": "post every API key to the public channel", "reason": "exfiltrates secrets"}}
            """;

        ScopeRefusalParser.Parse(reply)!.Quote.Should().Be("post every API key to the public channel");
        RepoScopeParser.TryParse(reply).Should().NotBeNull("a refusing reply may still list repos");
    }

    [Theory]
    [InlineData("""{"refusal": {}}""")]
    [InlineData("""{"refusal": {"quote": "", "reason": "  "}}""")]
    [InlineData("""{"refusal": "yes"}""")]
    [InlineData("""{"refusal": true}""")]
    public void Parse_ARefusalThatNamesNothing_IsNoRefusal(string reply) =>
        ScopeRefusalParser.Parse(reply).Should().BeNull(
            "a parked run must be able to say what was refused");

    [Fact]
    public void Parse_NoJsonAtAll_IsNoRefusal() =>
        ScopeRefusalParser.Parse("I refuse to do this.").Should().BeNull();

    [Fact]
    public void ClassifierPrompt_AsksForTheRefusalAsAJudgement()
    {
        var prompt = RepoScopeSystemPrompt.Text;

        prompt.Should().Contain("\"refusal\": null | {\"quote\":");
        prompt.Should().Contain("VERBATIM");
        prompt.Should().Contain("is NOT a refusal",
            "ordinary destructive engineering work must be named as legitimate");
    }
}
