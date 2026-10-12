using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-10-12-3a36e: the rule every new spec's repos is decided by, on its own.
/// 2026-10-12-f5f7a: in a scope of several repositories, exactly one per spec — no fill.</summary>
public sealed class SpecReposRuleTests
{
    private static readonly IReadOnlyList<string> Scope = ["BackgroundWorker", "Server"];
    private readonly SpecReposRule _rule = new();

    [Fact]
    public void SpecReposRule_MultiRepoScope_NoneStated_IsReAsked()
    {
        var resolution = _rule.Resolve([["Server"], []], Scope);

        resolution.Repos.Should().BeEmpty("the whole scope is a guess, so nothing is filled");
        resolution.NeedsSplit.Should().BeFalse();
        resolution.Error.Should().Contain("spec 2 names no repository").And.Contain("state it")
            .And.Contain("BackgroundWorker, Server");
    }

    [Fact]
    public void SpecReposRule_MultiRepoScope_SpecNamingTwo_IsReAskedToSplit()
    {
        var resolution = _rule.Resolve([["Server", "BackgroundWorker"]], Scope);

        resolution.NeedsSplit.Should().BeTrue();
        resolution.Error.Should().Contain("spec 1 names 2 repositories").And.Contain("split it")
            .And.Contain("depends on the other second");
    }

    [Fact]
    public void SpecReposRule_SingleRepoScope_IsFilled()
    {
        var resolution = _rule.Resolve([[], []], ["Server"]);

        resolution.Error.Should().BeNull();
        resolution.Repos.Should().AllSatisfy(r => r.Should().Equal("Server"));
    }

    [Fact]
    public void SpecReposResolution_SplitNeeded_IsARefusalThatSaysSplit()
    {
        var resolution = SpecReposResolution.SplitNeeded("why");

        resolution.Error.Should().Be("why");
        resolution.Repos.Should().BeEmpty();
        resolution.NeedsSplit.Should().BeTrue();
        SpecReposResolution.Refused("why").NeedsSplit.Should().BeFalse();
    }

    [Fact]
    public void Resolve_StatedInScope_IsKeptInTheScopesSpelling()
    {
        var resolution = _rule.Resolve([["SERVER", "server"]], Scope);

        resolution.Stated.Should().BeTrue();
        resolution.Repos[0].Should().Equal("Server");
    }

    [Fact]
    public void Resolve_OneRepositoryInScope_IsFilledWhateverWasStated()
    {
        _rule.Resolve([["Other"]], ["Server"]).Repos[0].Should().Equal("Server");
    }

    [Fact]
    public void Resolve_NoScope_PassesTheStatementThrough()
    {
        _rule.Resolve([["Anything"]], []).Repos[0].Should().Equal("Anything");
    }

    [Fact]
    public void ContextDisagreement_BareOrAgreeingContexts_IsNull()
    {
        _rule.ContextDisagreement(["Server"], ["api", "Server/api", "server/jobs"], 1).Should().BeNull();
    }

    [Fact]
    public void ContextDisagreement_ContextOfAnUnnamedRepository_NamesIt()
    {
        _rule.ContextDisagreement(["Server"], ["BackgroundWorker/jobs"], 3)
            .Should().Contain("spec 3").And.Contain("'BackgroundWorker'");
    }
}
