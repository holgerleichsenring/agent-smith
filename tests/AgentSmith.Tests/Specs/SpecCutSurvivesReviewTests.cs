using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0447: a cut the reviewer still objects to after the last attempt is worth more than
/// no cut at all.
/// <para>
/// The deriver retries three times, hands the objections back each time, and then throws
/// the cut away. What replaces it is one spec carrying the whole ticket — no boundaries,
/// no per-spec verdict, no repair pass, and on live run 2552 nineteen minutes of work
/// with no way to end. Four specs with an objection against the fourth are strictly
/// better than that, and the objection is recorded rather than obeyed.
/// </para>
/// <para>
/// The fail-safe itself is untouched: a cut that never PARSED is not a cut, and there is
/// nothing to keep.
/// </para>
/// </summary>
public sealed class SpecCutSurvivesReviewTests
{
    private const string Ticket = """
        Migrate every service off the legacy client.

        The `LegacyHttpHelper` API is forbidden in new code.

        Thanks in advance.
        """;

    [Fact]
    public async Task ACutTheReviewerStillObjectsTo_IsKeptRatherThanDiscarded()
    {
        var (derivation, error) = await Derive(ValidCut(), Objecting);

        derivation.Should().NotBeNull(
            "two specs with one objection beat one spec carrying the whole ticket");
        derivation!.Set.Specs.Should().HaveCount(2);
        error.Should().Contain("p19106b").And.Contain("not-in-ticket",
            "keeping the cut does not mean hiding what was found against it");
    }

    [Fact]
    public async Task ACutThatNeverParsed_LeavesNothingToKeep()
    {
        var (derivation, error) = await Derive("not json at all", Objecting);

        derivation.Should().BeNull("the fail-safe still catches a cut that is not a cut");
        error.Should().NotBeNull();
    }

    [Fact]
    public async Task ACleanCut_IsReturnedWithoutAnObjection()
    {
        var (derivation, error) = await Derive(ValidCut(), SpecCutReview.Clean);

        derivation.Should().NotBeNull();
        error.Should().BeNull();
    }

    // 2026-10-13-9a59g: every review is published with its attempt, a refused one with its findings.
    [Fact]
    public async Task SpecSetDeriver_RefusedCut_PublishesFindings()
    {
        var published = new List<AgentSmith.Contracts.Events.RunEvent>();

        await Derive(ValidCut(), new SequenceReviewer(Objecting), Publisher(published));

        var reviews = published.OfType<AgentSmith.Contracts.Events.SpecCutReviewedEvent>().ToList();
        reviews.Select(r => r.Attempt).Should().Equal(1, 2, 3);
        reviews.Should().OnlyContain(r => r.Deliverable == AgentSmith.Contracts.Events.CutReviewDeliverables.Refused
                                          && r.RunId == RunId);
        reviews[0].Findings.Should().ContainSingle().Which.SpecId.Should().Be("p19106b");
    }

    [Fact]
    public async Task SpecSetDeriver_AcceptedCut_PublishesDeliverable()
    {
        var published = new List<AgentSmith.Contracts.Events.RunEvent>();

        await Derive(ValidCut(), new SequenceReviewer(Objecting, SpecCutReview.Clean), Publisher(published));

        var reviews = published.OfType<AgentSmith.Contracts.Events.SpecCutReviewedEvent>().ToList();
        reviews.Select(r => (r.Attempt, r.Deliverable)).Should().Equal(
            (1, AgentSmith.Contracts.Events.CutReviewDeliverables.Refused),
            (2, AgentSmith.Contracts.Events.CutReviewDeliverables.Accepted));
        reviews[1].Findings.Should().BeEmpty();
    }

    private const string RunId = "2026-10-13T1000-cut";

    private static AgentSmith.Contracts.Events.IEventPublisher Publisher(List<AgentSmith.Contracts.Events.RunEvent> sink)
    {
        var publisher = new Mock<AgentSmith.Contracts.Events.IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<AgentSmith.Contracts.Events.RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AgentSmith.Contracts.Events.RunEvent, CancellationToken>((e, _) => sink.Add(e))
            .Returns(Task.CompletedTask);
        return publisher.Object;
    }

    private static Task<(SpecDerivation? Derivation, string? Error)> Derive(string reply, SpecCutReview verdict) =>
        Derive(reply, new FixedReviewer(verdict), events: null);

    private static async Task<(SpecDerivation? Derivation, string? Error)> Derive(
        string reply, ISpecCutReviewer reviewer, AgentSmith.Contracts.Events.IEventPublisher? events)
    {
        var runContext = Mock.Of<AgentSmith.Contracts.Events.IRunContextAccessor>(c => c.CurrentRunId == RunId);
        var deriver = new SpecSetDeriver(
            AgentSmith.Tests.TestSupport.CutReviewSteps.Of(reviewer, events, runContext),
            new SpecDerivationCall(
                new SingleClientFactory(new AlwaysAnswers(reply)), new AsyncLocalRunContextAccessor(),
                DerivationTestLooks.Proof()),
            DerivationTestLooks.Factory(),
            new FixedPrompt(),
            DerivationTestParsers.Real(),
            new ScopedContextCoverage(),
            AgentSmith.Tests.TestSupport.HandbackTestDoubles.Gate(), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), AgentSmith.Tests.TestSupport.ReferenceCarryDoubles.Materializer(), NullLogger<SpecSetDeriver>.Instance);

        var ticket = new Ticket(
            id: new TicketId("19106"), title: "migrate", description: Ticket,
            acceptanceCriteria: null, status: "open", source: "test");
        return await deriver.DeriveAsync(
            ticket, TicketSegmenter.Segment(Ticket), previous: null, series: "2026-10-06-0a0a", cause: "initial derivation",
            repositories: [], new AgentConfig(), new PipelineContext(), CancellationToken.None);
    }

    private static SpecCutReview Objecting =>
        new([new CutFinding("p19106b", "LegacyHttpHelper appears in no new code.",
            "not-in-ticket", "the ticket does not ask for this", null)]);

    private static string ValidCut()
    {
        var segments = TicketSegmenter.Segment(Ticket);
        var head = segments[0].Id;
        var tail = segments.Count > 1 ? segments[1].Id : head;
        var discarded = string.Join(",", segments.Skip(2).Select(
            s => "{\"segment\": " + s.Id + ", \"reason\": \"a sign-off, not part of the work\"}"));
        return $$$"""
            {"specs": [
               {"slug": "swap-the-client", "goal": "Move every call site onto the new client",
                "steps": [{"id": "swap", "action": "Swap the call sites"}],
                "done": ["No call site uses the legacy client."], "carries": [{{{head}}}]},
               {"slug": "forbid-the-helper", "goal": "Remove the forbidden helper from new code",
                "steps": [{"id": "forbid", "action": "Drop the helper"}],
                "done": ["LegacyHttpHelper appears in no new code."], "carries": [{{{tail}}}]}],
             "discarded": [{{{discarded}}}],
             "ignored_instructions": [],
             "handback": {"case": "none", "reason": ""}}
            """;
    }

    private sealed class FixedReviewer(SpecCutReview verdict) : ISpecCutReviewer
    {
        public Task<SpecCutReview> ReviewAsync(
            IReadOnlyList<SpecDraft> drafts, string key, string? ticketText, DerivationLook? look,
            AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
            => Task.FromResult(verdict);
    }

    private sealed class SequenceReviewer(params SpecCutReview[] verdicts) : ISpecCutReviewer
    {
        private int _calls;

        public Task<SpecCutReview> ReviewAsync(
            IReadOnlyList<SpecDraft> drafts, string key, string? ticketText, DerivationLook? look,
            AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
            => Task.FromResult(verdicts[Math.Min(_calls++, verdicts.Length - 1)]);
    }

    private sealed class FixedPrompt : IPromptCatalog
    {
        public string Get(string name) => "cut the ticket";

        public string Render(string name, IReadOnlyDictionary<string, string> tokens) => Get(name);
    }

    private sealed class SingleClientFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null) => client;

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }

    private sealed class AlwaysAnswers(string answer) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
