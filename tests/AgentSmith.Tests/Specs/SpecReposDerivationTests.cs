using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-12-3a36e: every phase a derivation cuts names the repositories it changes. The
/// check is ARMED BY THE REPLY: a reply that states repos is held to the scope and to its
/// own repo/context names; a reply that states none — a catalog ahead of the release — is
/// filled with the scope; one repository in scope is filled and never re-asked.
/// </summary>
public sealed class SpecReposDerivationTests
{
    private const string Ticket = """
        Move the retry policy into the server.

        Keep the worker unchanged.
        """;

    private static readonly IReadOnlyList<string> TwoRepos = ["BackgroundWorker", "Server"];

    private readonly SpecDerivationParser _parser = DerivationTestParsers.Real();

    [Fact]
    public void SpecDerivationParser_SingleRepoScope_FilledFromScope()
    {
        var parsed = Parse(Reply("""["Elsewhere"]""", """["Elsewhere"]"""), ["Server"]);

        parsed.Error.Should().BeNull("one repository in scope leaves the model nothing to decide");
        parsed.Derivation!.Set.Phases.Should().OnlyContain(p => p.Draft.Repos.SequenceEqual(new[] { "Server" }));
    }

    [Fact]
    public void SpecDerivationParser_NoPhaseStatesRepos_FilledFromScope()
    {
        var parsed = Parse(Reply(null, null), TwoRepos);

        parsed.Error.Should().BeNull("a catalog that never emits repos still cuts");
        var phases = parsed.Derivation!.Set.Phases;
        phases.Should().OnlyContain(p => p.Draft.Repos.SequenceEqual(TwoRepos));
        phases[0].Draft.Yaml.Should().Contain("repos:", "the fill is written into the spec, not only onto the draft");
    }

    [Fact]
    public void SpecDerivationParser_StatedInScope_KeepsTheScopesSpelling()
    {
        var parsed = Parse(Reply("""["server"]""", """["Server", "BackgroundWorker"]"""), TwoRepos);

        parsed.Error.Should().BeNull();
        parsed.Derivation!.Set.Phases[0].Draft.Repos.Should().Equal("Server");
        parsed.Derivation.Set.Phases[1].Draft.Repos.Should().Equal("Server", "BackgroundWorker");
    }

    [Fact]
    public void SpecDerivationParser_MixedReposDeclaration_IsReAsked()
    {
        var parsed = Parse(Reply("""["Server"]""", null), TwoRepos);

        parsed.Derivation.Should().BeNull();
        parsed.Error.Should().Contain("spec 2").And.Contain("every spec must");
    }

    [Fact]
    public void SpecDerivationParser_RepoOutsideScope_IsReAsked()
    {
        var parsed = Parse(Reply("""["Server"]""", """["Dashboard"]"""), TwoRepos);

        parsed.Derivation.Should().BeNull();
        parsed.Error.Should().Contain("'Dashboard'").And.Contain("BackgroundWorker, Server");
    }

    [Fact]
    public void SpecDerivationParser_ContextOfOtherRepo_IsReAsked()
    {
        var parsed = Parse(
            Reply("""["Server"]""", """["Server"]""", firstContexts: """["BackgroundWorker/jobs"]"""), TwoRepos);

        parsed.Derivation.Should().BeNull();
        parsed.Error.Should().Contain("'BackgroundWorker/jobs'").And.Contain("'BackgroundWorker'");
    }

    [Fact]
    public void SpecDerivationParser_NoScope_ChecksAndFillsNothing()
    {
        var parsed = Parse(Reply(null, null), []);

        parsed.Error.Should().BeNull();
        parsed.Derivation!.Set.Phases.Should().OnlyContain(p => p.Draft.Repos.Count == 0);
    }

    [Fact]
    public void PhaseDraftReader_Repos_RoundTripsThroughRenderer()
    {
        var yaml = new DerivedPhaseYamlRenderer().Render(
            "2026-10-12-0a0aa", "Move the retry policy", [], [], ["The server retries."], "x.md", [], "1",
            repos: ["Server", "BackgroundWorker"]);

        new SpecDraftValidator(new PhaseSpecSchemaProvider()).ValidateYaml(yaml)
            .Should().BeOfType<SpecDraftValid>("the schema declares repos");
        new PhaseDraftReader().Read(yaml).Repos.Should().Equal("Server", "BackgroundWorker");
    }

    [Fact]
    public void PhaseDraftReader_SpecWithoutRepos_ReadsNone()
    {
        new PhaseDraftReader().Read("spec: p0001\ngoal: g\n").Repos.Should().BeEmpty();
    }

    [Fact]
    public void SpecFallback_WholeTicket_NamesScope()
    {
        var fallback = new SpecFallback(
            new SpecDraftValidator(new PhaseSpecSchemaProvider()), new PhaseDraftReader(), new DerivedPhaseYamlRenderer());
        var ticket = new Ticket(new TicketId("7"), "Move the retry policy", Ticket, null, "open", "test");

        var set = fallback.Build(
            "key", "2026-10-12-0a0a", ticket, TicketSegmenter.Segment(Ticket), ["done"], SpecSource.Derived, TwoRepos);

        set.Phases.Single().Draft.Repos.Should().Equal(TwoRepos);
    }

    [Fact]
    public void ScopeRepositoriesPromptSection_ListsTheScopesNames()
    {
        var rendered = ScopeRepositoriesPromptSection.Render(TwoRepos);

        rendered.Should().Contain("- BackgroundWorker").And.Contain("- Server").And.Contain("`repos`");
        ScopeRepositoriesPromptSection.Render(["Server"]).Should().Contain("One repository is in scope: Server");
        ScopeRepositoriesPromptSection.Render([]).Should().BeEmpty();
    }

    private SpecDerivationParser.Parsed Parse(string reply, IReadOnlyList<string> scope)
    {
        var segments = TicketSegmenter.Segment(Ticket);
        return _parser.Parse(
            reply, "key", "2026-10-12-0a0a", "7", segments, SpecSource.Derived, scope: scope);
    }

    private static string Reply(string? first, string? second, string? firstContexts = null)
    {
        var firstExtra = (first is null ? "" : $", \"repos\": {first}")
            + (firstContexts is null ? "" : $", \"contexts\": {firstContexts}");
        var secondExtra = second is null ? "" : $", \"repos\": {second}";
        return "{\"phases\": ["
            + "{\"slug\": \"move-the-policy\", \"goal\": \"Move the retry policy into the server\", "
            + "\"done\": [\"The server owns the retry policy.\"], \"carries\": [1]" + firstExtra + "}, "
            + "{\"slug\": \"keep-the-worker\", \"goal\": \"Leave the worker as it is\", "
            + "\"done\": [\"The worker is unchanged.\"], \"carries\": [2]" + secondExtra + "}], "
            + "\"discarded\": [], \"handback\": {\"case\": \"none\", \"reason\": \"\"}}";
    }
}
