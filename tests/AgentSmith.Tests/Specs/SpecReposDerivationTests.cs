using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-12-3a36e: every spec a derivation cuts names the repositories it changes, held to
/// the scope and to its own repo/context names; one repository in scope is filled and never
/// re-asked. 2026-10-12-f5f7a: over several repositories each spec names EXACTLY ONE — none
/// stated is asked to state it, several to split; the whole-scope fill is gone.
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
    public void SpecDerivationParser_MultiRepoNoSpecStatesRepos_IsReAsked()
    {
        var parsed = Parse(Reply(null, null), TwoRepos);

        parsed.Derivation.Should().BeNull("the whole scope is a guess, so nothing is filled");
        parsed.Error.Should().Contain("spec 1 names no repository").And.Contain("state it");
    }

    [Fact]
    public void SpecDerivationParser_StatedInScope_KeepsTheScopesSpelling()
    {
        var parsed = Parse(Reply("""["server"]""", """["BackgroundWorker"]"""), TwoRepos);

        parsed.Error.Should().BeNull();
        parsed.Derivation!.Set.Phases[0].Draft.Repos.Should().Equal("Server");
        parsed.Derivation.Set.Phases[1].Draft.Repos.Should().Equal("BackgroundWorker");
        parsed.Derivation.Set.Phases[1].Draft.Requires.Should().Equal(
            [parsed.Derivation.Set.Phases[0].PhaseId], "requires: follows position — the dependent spec comes second");
    }

    [Fact]
    public void SpecDerivationParser_SpecNamingTwo_IsReAskedToSplit()
    {
        var parsed = Parse(Reply("""["Server"]""", """["Server", "BackgroundWorker"]"""), TwoRepos);

        parsed.Derivation.Should().BeNull();
        parsed.Error.Should().Contain("spec 2 names 2 repositories").And.Contain("split it");
    }

    [Fact]
    public void SpecDerivationParser_MixedReposDeclaration_IsReAsked()
    {
        var parsed = Parse(Reply("""["Server"]""", null), TwoRepos);

        parsed.Derivation.Should().BeNull();
        parsed.Error.Should().Contain("spec 2 names no repository");
    }

    [Fact]
    public void SpecDerivationEnvelope_ReposAsString_ReadsOne()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""{"repos": "Server"}""");

        new SpecDerivationEnvelope().Repos(doc.RootElement).Should().Equal("Server");
        Parse(Reply("\"Server\"", "\"BackgroundWorker\""), TwoRepos).Derivation!.Set.Phases[1].Draft.Repos
            .Should().Equal(new[] { "BackgroundWorker" }, "the shape the rule asks for is not read as nothing stated");
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
    public void SpecDraftReader_Repos_RoundTripsThroughRenderer()
    {
        var yaml = new DerivedSpecYamlRenderer().Render(
            "2026-10-12-0a0aa", "Move the retry policy", [], [], ["The server retries."], "x.md", [], "1",
            repos: ["Server", "BackgroundWorker"]);

        new SpecDraftValidator(new SpecSchemaProvider()).ValidateYaml(yaml)
            .Should().BeOfType<SpecDraftValid>("the schema declares repos");
        new SpecDraftReader().Read(yaml).Repos.Should().Equal("Server", "BackgroundWorker");
    }

    [Fact]
    public void SpecDraftReader_SpecWithoutRepos_ReadsNone()
    {
        new SpecDraftReader().Read("spec: p0001\ngoal: g\n").Repos.Should().BeEmpty();
    }

    [Fact]
    public void SpecFallback_MultiRepoScope_HandsBackNamingTheRepositories()
    {
        var set = Fallback(TwoRepos);

        set.Phases.Should().BeEmpty("the whole ticket in one repository's spec would be a guess");
        set.IsHandedBack.Should().BeTrue();
        set.Handback!.Case.Should().Be(SpecHandbackCase.ScopeUnassigned);
        set.Handback.Reason.Should().Contain("BackgroundWorker, Server");
        set.Series.Should().Be("2026-10-12-0a0a");
    }

    [Fact]
    public void SpecFallback_SingleRepoScope_WholeTicketNamesIt()
    {
        var set = Fallback(["Server"]);

        set.Phases.Single().Draft.Repos.Should().Equal("Server");
        set.TicketPinnedWhole.Should().BeTrue();
    }

    [Fact]
    public void SpecScopeHandback_For_ParksTheTicketNamingTheScope()
    {
        var set = SpecScopeHandback.For("key", "2026-10-12-0a0a", SpecSource.Derived, TwoRepos);

        set.Handback!.Reason.Should().Contain("BackgroundWorker, Server").And.Contain("exactly one");
        set.Handback.IsVerdict.Should().BeFalse("a reply and a re-trigger cut again");
        set.Accounting.Should().Be(SpecAccounting.Empty);
    }

    [Fact]
    public void ScopeRepositoriesPromptSection_ListsTheScopesNames()
    {
        var rendered = ScopeRepositoriesPromptSection.Render(TwoRepos);

        rendered.Should().Contain("- BackgroundWorker").And.Contain("- Server").And.Contain("`repos` EXACTLY ONE")
            .And.Contain("cut as two phases").And.NotContain("read as every phase changing all of them");
        ScopeRepositoriesPromptSection.Render(["Server"]).Should().Contain("One repository is in scope: Server");
        ScopeRepositoriesPromptSection.Render([]).Should().BeEmpty();
    }

    private static SpecSet Fallback(IReadOnlyList<string> scope)
    {
        var fallback = new SpecFallback(
            new SpecDraftValidator(new SpecSchemaProvider()), new SpecDraftReader(), new DerivedSpecYamlRenderer());
        var ticket = new Ticket(new TicketId("7"), "Move the retry policy", Ticket, null, "open", "test");
        return fallback.Build(
            "key", "2026-10-12-0a0a", ticket, TicketSegmenter.Segment(Ticket), ["done"], SpecSource.Derived, scope);
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
