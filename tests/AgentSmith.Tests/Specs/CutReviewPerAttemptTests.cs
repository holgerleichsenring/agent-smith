using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.CutReviewTestDoubles;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-15-ffa7: the reviewer keeps nothing between calls, so one allowance across the
/// deriver's attempts would let attempt one spend it and answer every later attempt "No look
/// left" to questions it has no record of having asked. Each attempt gets its own look.
/// </summary>
public sealed class CutReviewPerAttemptTests
{
    private const string Ticket = "Upgrade the vulnerable packages.\n\nThanks.";

    [Fact]
    public async Task Review_EachAttempt_GetsItsOwnAllowance()
    {
        var reviewer = new SpendingReviewer();
        var deriver = new SpecSetDeriver(
            reviewer,
            new SpecDerivationCall(
                new CappingFactory(new LookingProvider(Cut())), new AsyncLocalRunContextAccessor(), Proof()),
            Factory(), new FixedPrompt(), DerivationTestParsers.Real(), new ScopedContextCoverage(),
            NullLogger<SpecSetDeriver>.Instance);

        await deriver.DeriveAsync(
            new Ticket(new TicketId("1"), "upgrade", Ticket, null, "open", "test"),
            TicketSegmenter.Segment(Ticket), previous: null, cause: "initial derivation",
            new AgentConfig(), Pipeline(), CancellationToken.None);

        reviewer.Taken.Should().HaveCount(3).And.OnlyContain(n => n == DerivationLookTerms.CutReviewAllowance,
            "an attempt that finds the allowance spent by the one before it could not look at all");
        reviewer.Looks.Distinct().Should().HaveCount(3);
    }

    [Fact]
    public async Task Rejection_AtTheDeriver_CarriesTheReviewersEvidenceLine()
    {
        var provider = new LookingProvider(Cut());
        var deriver = new SpecSetDeriver(
            new CitingReviewer(),
            new SpecDerivationCall(new CappingFactory(provider), new AsyncLocalRunContextAccessor(), Proof()),
            Factory(), new FixedPrompt(), DerivationTestParsers.Real(), new ScopedContextCoverage(),
            NullLogger<SpecSetDeriver>.Instance);

        await deriver.DeriveAsync(
            new Ticket(new TicketId("1"), "upgrade", Ticket, null, "open", "test"),
            TicketSegmenter.Segment(Ticket), previous: null, cause: "initial derivation",
            new AgentConfig(), Pipeline(), CancellationToken.None);

        provider.Prompts.Should().HaveCountGreaterThan(1);
        provider.Prompts[1].Should().Contain(
            $"(evidence: [R1] {Repo}: the cut review ran 'read src/Bus.cs' exited 1)",
            "the deriver is answered with the line the reviewer's own look minted");
    }

    [Fact]
    public async Task Review_AtTheDeriver_IsShownTheTicketsTitleToo()
    {
        var reviewer = new SpendingReviewer();
        var deriver = new SpecSetDeriver(
            reviewer,
            new SpecDerivationCall(new CappingFactory(new LookingProvider(Cut())), new AsyncLocalRunContextAccessor(), Proof()),
            Factory(), new FixedPrompt(), DerivationTestParsers.Real(), new ScopedContextCoverage(),
            NullLogger<SpecSetDeriver>.Instance);

        await deriver.DeriveAsync(
            new Ticket(new TicketId("1"), "Raise the package floors", Ticket, "The audit is clean", "open", "test"),
            TicketSegmenter.Segment(Ticket), previous: null, cause: "initial derivation",
            new AgentConfig(), Pipeline(), CancellationToken.None);

        reviewer.Tickets[0].Should().Be($"Raise the package floors\n\n{Ticket}\n\nThe audit is clean");
    }

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)
            new Dictionary<string, ISandbox> { [Repo] = new CountingSandbox(0) });
        pipeline.Set(ContextKeys.SandboxDiscoveries,
            (IReadOnlyDictionary<string, RemoteContextDiscovery>)new Dictionary<string, RemoteContextDiscovery>());
        return pipeline;
    }

    private static string Cut()
    {
        var segments = TicketSegmenter.Segment(Ticket);
        return $$$"""
            {"phases": [
               {"slug": "raise-the-floors", "goal": "Raise the direct package floors",
                "steps": [{"id": "raise", "action": "Raise the versions"}],
                "done": ["The manifests carry versions the audit no longer flags."],
                "carries": [{{{segments[0].Id}}}]}],
             "discarded": [{"segment": {{{segments[^1].Id}}}, "reason": "a sign-off"}],
             "ignored_instructions": [],
             "handback": {"case": "none", "reason": ""}}
            """;
    }

    /// <summary>Spends whatever look it is handed, then objects so the deriver tries again.</summary>
    private sealed class SpendingReviewer : ISpecCutReviewer
    {
        public List<DerivationLook> Looks { get; } = [];
        public List<int> Taken { get; } = [];
        public List<string?> Tickets { get; } = [];

        public Task<SpecCutReview> ReviewAsync(
            IReadOnlyList<PhaseDraft> drafts, string key, string? ticketText, DerivationLook? look,
            AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
        {
            Looks.Add(look!);
            Tickets.Add(ticketText);
            var taken = 0;
            while (look!.Budget.TryTake()) taken++;
            Taken.Add(taken);
            return Task.FromResult(new SpecCutReview([new CutFinding(
                drafts[0].PhaseId, drafts[0].Done[0], SpecCutVerdicts.Uncheckable, "objection")]));
        }
    }

    /// <summary>Takes one look on the look it is handed and reports a false premise citing it.</summary>
    private sealed class CitingReviewer : ISpecCutReviewer
    {
        public Task<SpecCutReview> ReviewAsync(
            IReadOnlyList<PhaseDraft> drafts, string key, string? ticketText, DerivationLook? look,
            AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
        {
            var id = look!.Evidence.Remember(new EvidenceRecord(Repo, EvidenceRecord.Read, "read src/Bus.cs", 1, Ran: true));
            return Task.FromResult(new SpecCutReview([new CutFinding(
                drafts[0].PhaseId, "the bus is registered", SpecCutVerdicts.FalsePremise, "it is not", Cites: id)]));
        }
    }

    private sealed class FixedPrompt : IPromptCatalog
    {
        public string Get(string name) => "cut the ticket";

        public string Render(string name, IReadOnlyDictionary<string, string> tokens) => Get(name);
    }
}
