using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-15-c6e9: the init pull request body says what the round DID.
/// <para>
/// It was one literal, equally true of a run that transferred principles into an empty
/// repository and of one that preserved a ratified file and wrote nothing. The pull request is
/// the artefact a human opens to ratify what the framework wrote.
/// </para>
/// </summary>
public sealed class InitPullRequestBodyTests
{
    private const string Marker = "<!-- agentsmith:sibling-prs -->";
    private const string Literal = "Auto-generated project context, code map, and coding principles.";

    [Fact]
    public void InitPullRequestBody_ComposedFromTheRound_NotALiteral()
    {
        var body = Compose(Outcome("api", "server", PrinciplesMode.Transferred,
            new ArtefactWrite(".editorconfig", ArtefactStatus.Written)));

        body.Should().Contain("server", "the body names the context the round worked on");
        body.Should().Contain(".editorconfig", "an artefact the run wrote is named where it is ratified");
        body.Should().NotBe($"{Literal}\n\n{Marker}", "that is the literal this phase replaced");
    }

    [Fact]
    public void InitPullRequestBody_PreservedPrinciples_SaysSoRatherThanClaimingTransfer()
    {
        var body = Compose(Outcome("api", "server", PrinciplesMode.PreservedExisting));

        body.Should().Contain("left untouched");
        body.Should().NotContain("transferred",
            "claiming a transfer that did not happen is the failure the literal already had");
    }

    [Fact]
    public void InitPullRequestBody_TwoComponentsOfOneRepo_BothAppear()
    {
        var body = Compose(
            Outcome("api", "server", PrinciplesMode.Transferred),
            Outcome("api", "client", PrinciplesMode.Transferred));

        body.Should().Contain("server").And.Contain("client",
            "a repository fans out one round per component; a body that reported one would "
            + "silently drop the rest");
    }

    [Fact]
    public void InitPullRequestBody_OtherReposOutcomes_DoNotLeakIntoThisOne()
    {
        var body = Compose(
            Outcome("api", "server", PrinciplesMode.Transferred),
            Outcome("web", "frontend", PrinciplesMode.Transferred));

        body.Should().Contain("server");
        body.Should().NotContain("frontend", "each pull request describes its own repository");
    }

    [Fact]
    public void InitPullRequestBody_ArtefactWrittenForAnEarlierContext_IsNotCalledRatified()
    {
        var body = Compose(Outcome("api", "client", PrinciplesMode.Transferred,
            new ArtefactWrite(".editorconfig", ArtefactStatus.AlreadyWrittenThisRound)));

        body.Should().Contain("earlier context of this repository");
        body.Should().NotContain("already present",
            "the operator ratified nothing — this run wrote it, for the first component");
    }

    [Fact]
    public void InitPullRequestBody_RefusedArtefact_NamesTheReason()
    {
        var body = Compose(Outcome("api", "server", PrinciplesMode.Transferred,
            new ArtefactWrite("../escape", ArtefactStatus.Refused, "File path must not contain parent traversal.")));

        body.Should().Contain("NOT written").And.Contain("parent traversal");
    }

    [Fact]
    public void InitPullRequestBody_AlwaysCarriesTheCrossLinkMarker()
    {
        // PrCrossLinkHandler replaces this marker with the sibling list and fails hard when it
        // is absent, so composing a body without it breaks the pull-request chain.
        Compose().Should().EndWith(Marker, "a run that bootstrapped nothing still gets cross-linked");
        Compose(Outcome("api", "server", PrinciplesMode.Transferred)).Should().EndWith(Marker);
    }

    [Fact]
    public void InitPullRequestBody_NoBootstrapRan_ReadsAsItAlwaysDid()
    {
        Compose().Should().Be($"{Literal}\n\n{Marker}",
            "a pipeline with no bootstrap round has nothing new to report");
    }

    private static string Compose(params BootstrapRoundOutcome[] outcomes) =>
        InitPullRequestBody.Compose(outcomes, "api", Marker);

    private static BootstrapRoundOutcome Outcome(
        string repo, string context, PrinciplesMode mode, params ArtefactWrite[] artefacts) =>
        new(repo, context, mode, artefacts);
}
