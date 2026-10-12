using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-10-12-3a36e: the rule every new spec's repos is decided by, on its own.</summary>
public sealed class SpecReposRuleTests
{
    private static readonly IReadOnlyList<string> Scope = ["BackgroundWorker", "Server"];
    private readonly SpecReposRule _rule = new();

    [Fact]
    public void Resolve_NothingStated_FillsEverySpecWithTheScope()
    {
        var resolution = _rule.Resolve([[], []], Scope);

        resolution.Error.Should().BeNull();
        resolution.Stated.Should().BeFalse("a fill arms no further check");
        resolution.Repos.Should().AllSatisfy(r => r.Should().Equal(Scope));
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
