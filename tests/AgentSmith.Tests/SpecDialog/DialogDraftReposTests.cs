using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-12-3a36e: a design dialog's lone spec and every epic child name the repositories
/// they change, within the turn's scope. The epic parent is the umbrella ticket and names none.
/// 2026-10-12-f5f7a: over several repositories each names EXACTLY ONE — nothing is filled, and a
/// lone spec naming several is told to become an epic, one child per repository.
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

        var draft = Spec(_resolver.Resolve(reply, Scope));

        draft.Repos.Should().Equal("Server");
        draft.Yaml.Should().Contain("# the server only", "a stated, valid field leaves the text as written");
    }

    [Fact]
    public void OutcomeProposalResolver_MultiRepoLoneSpecWithoutRepos_IsInvalid()
    {
        _resolver.Resolve(Lone(string.Empty), Scope).Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("names no repository").And.Contain("state it");
    }

    [Fact]
    public void DialogDraftRepos_LoneSpecNamingTwo_NudgesEpic()
    {
        var (yaml, error) = DialogTestRepos.Real().ForSpec(
            "spec: p9999\ngoal: g\nrepos: [Server, BackgroundWorker]\n", Scope);

        yaml.Should().BeNull();
        error.Should().Contain("names 2 repositories").And.Contain("propose it as an epic, one child per repository");
    }

    [Fact]
    public void OutcomeProposalResolver_LoneSpecNamingOneAsString_IsKept()
    {
        Spec(_resolver.Resolve(Lone("repos: Server"), Scope)).Repos.Should().Equal("Server");
    }

    [Fact]
    public void OutcomeProposalResolver_OneRepoScope_RewritesAStatedOther()
    {
        var draft = Spec(_resolver.Resolve(Lone("repos:\n- Elsewhere"), ["Server"]));

        draft.Repos.Should().Equal("Server");
        draft.Yaml.Should().NotContain("Elsewhere");
    }

    [Fact]
    public void OutcomeProposalResolver_MultiRepoEpicChildrenWithoutRepos_AreInvalid()
    {
        _resolver.Resolve(Epic(null, null), Scope).Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("epic children").And.Contain("spec 1 names no repository");
    }

    [Fact]
    public void OutcomeProposalResolver_EpicOneChildPerRepository_KeepsThemAndTheParentNamesNone()
    {
        var epic = _resolver.Resolve(Epic("repos: [Server]", "repos: [BackgroundWorker]"), Scope)
            .Should().BeOfType<OutcomeResolved>().Which.Proposal.Should().BeOfType<EpicOutcome>().Subject;

        epic.Children[0].Repos.Should().Equal("Server");
        epic.Children[1].Repos.Should().Equal("BackgroundWorker");
        epic.Parent.Repos.Should().BeEmpty("the umbrella ticket changes nothing itself");
    }

    [Fact]
    public void OutcomeProposalResolver_SingleRepoEpicChildrenWithoutRepos_AreFilled()
    {
        var epic = _resolver.Resolve(Epic(null, null), ["Server"]).Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<EpicOutcome>().Subject;

        epic.Children.Should().OnlyContain(c => c.Repos.SequenceEqual(new[] { "Server" }));
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
            .Should().BeOfType<OutcomeInvalid>().Which.Error.Should().Contain("spec 2 names no repository");
    }

    [Fact]
    public void SpecDraftReposWriter_StatedBlock_IsReplacedAndTheRestKept()
    {
        var written = new SpecDraftReposWriter().Write(
            "spec: p1\nrepos:\n  - Old\n  - Older\ngoal: g # kept\n", ["Server"]);

        written.Should().Be("spec: p1\ngoal: g # kept\nrepos:\n- Server\n");
    }

    private static SpecDraft Spec(OutcomeResolution resolution) =>
        resolution.Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<SpecOutcome>().Subject.Draft;

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
        var validator = new SpecDraftValidator(new SpecSchemaProvider());
        var reader = new SpecDraftReader();
        return new OutcomeProposalResolver(
            validator, reader, new BugOutcomeParser(),
            new EpicOutcomeParser(validator, reader, new RequiresEdgeChecker(), DialogTestRepos.Real(), AgentSmith.Tests.TestSupport.SeriesContracts.Reader(), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader()),
            new TicketGraphOutcomeParser(new TicketGraphChecker(new TicketGraphOrderer())),
            DialogTestRepos.Real());
    }
}
