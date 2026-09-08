using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-08-1830: the real deriver over a scripted client. A deliverable cut that
/// covers fewer contexts than the scope call named is pinned back once, in the same
/// conversation; a second cut that carries the gap is accepted, and one that still does
/// not becomes a question for the author. A covered cut costs the one call it always did.
/// </summary>
public sealed class SpecDerivationCoverageTests
{
    private const string Ticket = """
        Fix the high and critical advisories in the dependencies of the backend and the frontend.

        Thanks.
        """;

    [Fact]
    public async Task Deriver_AGap_IsPinnedAndTheSecondCutCarriesIt()
    {
        var client = new ScriptedClient(Cut("frontend"), Cut("frontend", "backend"));

        var (derivation, error) = await Derive(client, Named("frontend", "backend"));

        error.Should().BeNull();
        derivation!.Set.IsHandedBack.Should().BeFalse("the second cut carried the gap");
        derivation.Set.Phases.Should().ContainSingle().Which.Draft.Contexts.Should().Equal("frontend", "backend");
        client.Calls.Should().Be(2, "one extra call, not a fresh derivation");
        var pin = client.MessagesOfCall(1).Last(m => m.Role == ChatRole.User).Text;
        pin.Should().Contain("## Your cut does not cover every context the scope call named");
        pin.Should().Contain("named `backend`").And.Contain("carries `frontend` only");
    }

    [Fact]
    public async Task Deriver_AGapThatPersists_HandsBackAQuestion()
    {
        var client = new ScriptedClient(Cut("frontend"), Cut("frontend"));

        var (derivation, error) = await Derive(client, Named("frontend", "backend"));

        error.Should().BeNull();
        var handback = derivation!.Set.Handback!;
        handback.Case.Should().Be(SpecHandbackCase.Question);
        handback.Readings.Should().Equal("carry backend as well", "backend is out of scope for this ticket");
        handback.TakenReading.Should().Be("carry backend as well");
        handback.Reason.Should().Contain("both contexts need dependency auditing", "the reason names the scope call's rationale");
        derivation.Set.Phases.Should().BeEmpty("a hand-back replaces the spec");
        client.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Deriver_ADiscardedContext_ClosesTheGapWithoutAPin()
    {
        var client = new ScriptedClient(CutLeavingOut("frontend", "backend"));

        var (derivation, _) = await Derive(client, Named("frontend", "backend"));

        derivation!.Set.IsHandedBack.Should().BeFalse();
        derivation.Set.Accounting.DiscardedContexts.Should().ContainSingle().Which.Context.Should().Be("backend");
        client.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Deriver_NoGap_CostsOneCall()
    {
        var client = new ScriptedClient(Cut("frontend", "backend"));

        var (derivation, _) = await Derive(client, Named("frontend", "backend"));

        derivation!.Set.IsHandedBack.Should().BeFalse();
        client.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Deriver_ACutDeclaringNoContexts_IsAcceptedAsBefore()
    {
        // An old catalog never emits the field; its cut is read exactly as before.
        var client = new ScriptedClient(Cut());

        var (derivation, error) = await Derive(client, Named("frontend", "backend"));

        error.Should().BeNull();
        derivation!.Set.IsHandedBack.Should().BeFalse();
        client.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Deriver_TheNamedContexts_ReachTheDerivationPrompt()
    {
        var client = new ScriptedClient(Cut("frontend", "backend"));

        await Derive(client, Named("frontend", "backend"));

        var prompt = client.MessagesOfCall(0).First(m => m.Role == ChatRole.User).Text;
        prompt.Should().Contain("## Contexts the scope call named").And.Contain("- backend");
    }

    private static ScopeNamedContexts Named(params string[] contexts) =>
        new(contexts, "both contexts need dependency auditing");

    private static async Task<(SpecDerivation? Derivation, string? Error)> Derive(
        ScriptedClient client, ScopeNamedContexts named)
    {
        var deriver = new SpecSetDeriver(
            new CleanReviewer(),
            new SpecDerivationCall(new SingleClientFactory(client), new AsyncLocalRunContextAccessor()),
            DerivationTestLooks.Factory(),
            new FixedPrompt(),
            DerivationTestParsers.Real(),
            new ScopedContextCoverage(),
            NullLogger<SpecSetDeriver>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ScopeNamedContexts, named);
        var ticket = new Ticket(
            id: new TicketId("19106"), title: "advisories", description: Ticket,
            acceptanceCriteria: null, status: "open", source: "test");
        return await deriver.DeriveAsync(
            ticket, TicketSegmenter.Segment(Ticket), previous: null, cause: "initial derivation",
            new AgentConfig(), pipeline, CancellationToken.None);
    }

    private static string Cut(params string[] contexts) => Cut(contexts, discarded: null);

    private static string CutLeavingOut(string context, string discarded) => Cut([context], discarded);

    private static string Cut(string[] contexts, string? discarded)
    {
        var segments = TicketSegmenter.Segment(Ticket);
        var declared = contexts.Length == 0
            ? string.Empty
            : "\"contexts\": [" + string.Join(", ", contexts.Select(c => $"\"{c}\"")) + "],";
        var left = discarded is null
            ? "[]"
            : $$$"""[{"context": "{{{discarded}}}", "reason": "the audit flags nothing there"}]""";
        return $$$"""
            {"phases": [
               {"slug": "raise-the-floors", "goal": "Raise the package floors the audit names",
                {{{declared}}}
                "steps": [{"id": "raise", "action": "Raise the versions"}],
                "done": ["The manifests carry versions the audit no longer flags."],
                "carries": [{{{segments[0].Id}}}]}],
             "discarded": [{"segment": {{{segments[^1].Id}}}, "reason": "a sign-off"}],
             "discarded_contexts": {{{left}}},
             "handback": {"case": "none", "reason": ""}}
            """;
    }

    /// <summary>Answers each call with the next scripted reply and keeps every call's messages.</summary>
    private sealed class ScriptedClient(params string[] replies) : IChatClient
    {
        private readonly List<IReadOnlyList<ChatMessage>> _calls = [];

        public int Calls => _calls.Count;

        public IReadOnlyList<ChatMessage> MessagesOfCall(int index) => _calls[index];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            _calls.Add([.. messages]);
            var reply = replies[Math.Min(_calls.Count - 1, replies.Length - 1)];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class CleanReviewer : ISpecCutReviewer
    {
        public Task<SpecCutReview> ReviewAsync(
            SpecSet set, string ticketText, AgentConfig agent,
            PipelineCostTracker costTracker, CancellationToken cancellationToken)
            => Task.FromResult(SpecCutReview.Clean);
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
}
