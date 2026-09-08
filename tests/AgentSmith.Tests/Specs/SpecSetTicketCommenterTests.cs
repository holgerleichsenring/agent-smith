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

        var body = SpecSetComment.Render(set, "https://example.test/pr/1");

        body.Should().StartWith(SpecSetComment.Marker);
        body.Should().Contain("p19106a — Raise the direct package floors the audit names");
        body.Should().Contain("- The manifests carry versions the audit no longer flags.");
        body.Should().Contain("- The build exits 0.");
        body.Should().Contain("- one direct package is affected").And.Contain($"_{Evidence}_",
            "a fact renders beside the look it cites");
        body.Should().Contain("- every other finding is transitive");
        body.Should().Contain("Assumptions");
        body.Should().Contain("## Discarded from the ticket").And.Contain("a sign-off");
        body.Should().Contain("https://example.test/pr/1");
    }

    // 2026-09-08-1830: the author sees which contexts each phase changes and which named
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
        var commenter = new SpecSetTicketCommenter(factory.Object, NullLogger<SpecSetTicketCommenter>.Instance);

        await commenter.PostAsync(pipeline, new TrackerConnection(), Set(), CancellationToken.None);

        provider.Verify(p => p.UpdateStatusAsync(
            It.IsAny<TicketId>(),
            It.Is<string>(c => c.Contains("Done when:") && c.Contains(Evidence)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

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
            reply, "azdo-19106", "19106", segments, SpecSource.Derived, null, [Evidence]);
        parsed.Error.Should().BeNull();
        return parsed.Derivation!.Set;
    }
}
