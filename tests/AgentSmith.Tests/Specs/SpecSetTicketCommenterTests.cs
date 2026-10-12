using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-07-b7e2: the author cannot object to what they never see. The derivation-time
/// comment carries the criteria, the facts and the assumptions beside the goals and the
/// discarded segments it already showed.
/// </summary>
public sealed class SpecSetTicketCommenterTests
{
    private const string Ticket = """
        Upgrade the vulnerable packages.

        Thanks.
        """;

    private const string Evidence =
        "[L1] api: the derivation ran 'dotnet list package --vulnerable --format json' exited 0";

    [Fact]
    public void Comment_TheDerivationTimeComment_CarriesEveryDoneCriterionFactAndAssumption()
    {
        var set = Set();

        var body = SpecSetComment.Render(set, new Dictionary<string, string> { ["api"] = "https://example.test/pr/1" });

        body.Should().StartWith(SpecSetComment.Marker);
        body.Should().Contain("2026-10-06-9106a — Raise the direct package floors the audit names");
        body.Should().Contain("- The manifests carry versions the audit no longer flags.");
        body.Should().Contain("- The build exits 0.");
        body.Should().Contain("- one direct package is affected").And.Contain($"_{Evidence}_",
            "a fact renders beside the look it cites");
        body.Should().Contain("- every other finding is transitive");
        body.Should().Contain("Assumptions");
        body.Should().Contain("## Discarded from the ticket").And.Contain("a sign-off");
        body.Should().Contain("https://example.test/pr/1");
    }

    // 2026-09-08-1830: the author sees which contexts each spec changes and which named
    // context the cut left out, with the reason — the part run 8688's author never saw.
    [Fact]
    public void Comment_TheDerivationTimeComment_CarriesContextsAndTheOnesLeftOut()
    {
        var set = Set(contexts: "\"contexts\": [\"frontend\"],",
            discardedContexts: """[{"context": "backend", "reason": "the audit flags nothing there"}]""");

        var body = SpecSetComment.Render(set, null);

        body.Should().Contain("**Contexts:** frontend");
        body.Should().Contain("## Contexts left out").And.Contain("- backend: the audit flags nothing there");
        body.Should().Contain("## Discarded from the ticket", "the segment block stays");
    }

    [Fact]
    public void Comment_ACutDeclaringNoContexts_RendersNoContextsBlock()
    {
        var body = SpecSetComment.Render(Set(), null);

        body.Should().NotContain("Contexts left out").And.NotContain("**Contexts:**");
    }

    // 2026-10-02-3f06a: an assumption written as {claim, check} reaches the author as its claim,
    // never as the type name the map's ToString() used to render.
    [Fact]
    public void SpecSetComment_AssumptionMap_RendersItsClaim()
    {
        var set = Set();
        var read = new SpecDraftReader().Read(
            "spec: p19106a\ngoal: g\nassumptions:\n  - claim: \"the cache is warm\"\n    check: \"read it\"\n");
        var spec = set.Specs[0] with { Draft = set.Specs[0].Draft with { Assumptions = read.Assumptions } };

        var body = SpecSetComment.Render(set with { Specs = [spec] }, null);

        body.Should().Contain("- the cache is warm").And.NotContain("System.Collections");
    }

    [Fact]
    public async Task Comment_IsPostedToTheTicket_WithTheRenderedBody()
    {
        var provider = new Mock<ITicketProvider>();
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, new Ticket(
            id: new TicketId("19106"), title: "upgrade", description: Ticket,
            acceptanceCriteria: null, status: "open", source: "test"));
        var commenter = new SpecSetTicketCommenter(factory.Object, Moq.Mock.Of<AgentSmith.Contracts.Reviews.IFullSetPrNotice>(), NullLogger<SpecSetTicketCommenter>.Instance);

        await commenter.PostAsync(pipeline, new TrackerConnection(), Set(), CancellationToken.None);

        provider.Verify(p => p.UpdateStatusAsync(
            It.IsAny<TicketId>(),
            It.Is<string>(c => c.Contains("Done when:") && c.Contains(Evidence)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // 2026-10-12-f5f7e: every series repository has its own spec PR; a repository the set no longer
    // names still has its open one, which the comment lists as dropped.
    [Fact]
    public async Task SpecSetTicketCommenter_TwoSpecPrs_ListsBoth()
    {
        var provider = new Mock<ITicketProvider>();
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, new Ticket(
            id: new TicketId("19106"), title: "upgrade", description: Ticket,
            acceptanceCriteria: null, status: "open", source: "test"));
        pipeline.Set(ContextKeys.SpecPullRequestUrls, new Dictionary<string, string>
        {
            ["Server"] = "https://x/server/pull/1", ["Client"] = "https://x/client/pull/2", ["Worker"] = "https://x/worker/pull/3",
        });
        var commenter = new SpecSetTicketCommenter(factory.Object, Moq.Mock.Of<AgentSmith.Contracts.Reviews.IFullSetPrNotice>(), NullLogger<SpecSetTicketCommenter>.Instance);

        await commenter.PostAsync(pipeline, new TrackerConnection(), Set() with { SeriesRepos = ["Server", "Client"] }, CancellationToken.None);

        provider.Verify(p => p.UpdateStatusAsync(It.IsAny<TicketId>(), It.Is<string>(c =>
            c.Contains("- Server: https://x/server/pull/1") && c.Contains("- Client: https://x/client/pull/2")
            && c.Contains("- Worker: https://x/worker/pull/3 — dropped from the series")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SpecPullRequestLines_OneMember_IsTheSentenceItAlwaysWas() =>
        SpecPullRequestLines.Render(new Dictionary<string, string> { ["Server"] = "https://x/pull/1" }, ["Server"], "Open")
            .Should().Be("Open: https://x/pull/1");

    private static SpecSet Set(string contexts = "", string discardedContexts = "[]")
    {
        var segments = TicketSegmenter.Segment(Ticket);
        var reply = $$$"""
            {"phases": [
               {"slug": "raise-the-floors", "goal": "Raise the direct package floors the audit names",
                {{{contexts}}}
                "steps": [{"id": "raise", "action": "Raise the versions"}],
                "done": ["The manifests carry versions the audit no longer flags.", "The build exits 0."],
                "carries": [{{{segments[0].Id}}}],
                "facts": [{"claim": "one direct package is affected", "cites": "L1"},
                          {"claim": "every other finding is transitive", "cites": ""}] }],
             "discarded": [{"segment": {{{segments[^1].Id}}}, "reason": "a sign-off"}],
             "discarded_contexts": {{{discardedContexts}}},
             "ignored_instructions": [],
             "handback": {"case": "none", "reason": ""}}
            """;
        var parsed = DerivationTestParsers.Real().Parse(
            reply, "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived, null, [Evidence]);
        parsed.Error.Should().BeNull();
        return parsed.Derivation!.Set;
    }
}
