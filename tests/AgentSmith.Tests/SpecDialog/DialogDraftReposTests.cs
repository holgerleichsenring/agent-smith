using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-12-3a36e: a design dialog's lone spec and every epic child name the repositories
/// they change — stated within the turn's scope, or filled from it. The epic parent is the
/// umbrella ticket and names none.
/// </summary>
public sealed class DialogDraftReposTests
{
    private static readonly IReadOnlyList<string> Scope = ["BackgroundWorker", "Server"];

    private readonly OutcomeProposalResolver _resolver = Resolver();

    [Fact]
    public void OutcomeProposalResolver_RepoOutsideScope_IsInvalid()
    {
        var resolution = _resolver.Resolve(Lone("repos: [Dashboard]"), Scope);

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("'Dashboard'").And.Contain("BackgroundWorker, Server");
    }

    [Fact]
    public void OutcomeProposalResolver_LoneSpecInScope_KeepsTheModelsText()
    {
        var reply = Lone("repos: [Server]  # the server only");

        var draft = Phase(_resolver.Resolve(reply, Scope));

        draft.Repos.Should().Equal("Server");
        draft.Yaml.Should().Contain("# the server only", "a stated, valid field leaves the text as written");
    }

    [Fact]
    public void OutcomeProposalResolver_LoneSpecWithoutRepos_IsFilledFromScope()
    {
        var draft = Phase(_resolver.Resolve(Lone(string.Empty), Scope));

        draft.Repos.Should().Equal(Scope);
        draft.Yaml.Should().Contain("repos:").And.Contain("goal: \"Add the widget endpoint\"");
    }

    [Fact]
    public void OutcomeProposalResolver_OneRepoScope_RewritesAStatedOther()
    {
        var draft = Phase(_resolver.Resolve(Lone("repos:\n- Elsewhere"), ["Server"]));

        draft.Repos.Should().Equal("Server");
        draft.Yaml.Should().NotContain("Elsewhere");
    }

    [Fact]
    public void OutcomeProposalResolver_EpicChildrenWithoutRepos_AreFilledAndTheParentIsNot()
    {
        var epic = _resolver.Resolve(Epic(null, null), Scope).Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<EpicOutcome>().Subject;

        epic.Children.Should().OnlyContain(c => c.Repos.SequenceEqual(Scope));
        epic.Parent.Repos.Should().BeEmpty("the umbrella ticket changes nothing itself");
    }

    [Fact]
    public void OutcomeProposalResolver_EpicChildOutsideScope_IsInvalid()
    {
        _resolver.Resolve(Epic("repos: [Server]", "repos: [Dashboard]"), Scope)
            .Should().BeOfType<OutcomeInvalid>().Which.Error.Should().Contain("epic children").And.Contain("spec 2");
    }

    [Fact]
    public void OutcomeProposalResolver_EpicMixedDeclaration_IsInvalid()
    {
        _resolver.Resolve(Epic("repos: [Server]", null), Scope)
            .Should().BeOfType<OutcomeInvalid>().Which.Error.Should().Contain("every spec must");
    }

    [Fact]
    public void PhaseDraftReposWriter_StatedBlock_IsReplacedAndTheRestKept()
    {
        var written = new PhaseDraftReposWriter().Write(
            "spec: p1\nrepos:\n  - Old\n  - Older\ngoal: g # kept\n", ["Server"]);

        written.Should().Be("spec: p1\ngoal: g # kept\nrepos:\n- Server\n");
    }

    private static PhaseDraft Phase(OutcomeResolution resolution) =>
        resolution.Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<PhaseOutcome>().Subject.Draft;

    private static string Lone(string repos) => $"""
        Draft:
        ```yaml
        spec: p9999
        goal: "Add the widget endpoint"
        {repos}
        ```
        """;

    private static string Epic(string? child1Repos, string? child2Repos) => $"""
        ```outcome
        kind: epic
        parent:
          spec: p9000
          goal: "Widget platform end to end"
        children:
          - spec: p9000a
            goal: "Widget storage layer"
            done: ["a widget is stored"]
            {child1Repos}
          - spec: p9000b
            goal: "Widget API"
            done: ["the API returns a stored widget"]
            {child2Repos}
        ```
        """;

    private static OutcomeProposalResolver Resolver()
    {
        var validator = new SpecDraftValidator(new PhaseSpecSchemaProvider());
        var reader = new PhaseDraftReader();
        return new OutcomeProposalResolver(
            validator, reader, new BugOutcomeParser(),
            new EpicOutcomeParser(validator, reader, new RequiresEdgeChecker(), DialogTestRepos.Real()),
            DialogTestRepos.Real());
    }
}
