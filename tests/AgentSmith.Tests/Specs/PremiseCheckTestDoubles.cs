using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79c: the premise check as the product composes it — the real handler over the
/// real checker, the real look factory and a provider that looks as scripted and then answers.
/// </summary>
internal static class PremiseCheckTestDoubles
{
    public const string PhaseId = "2026-09-17-1111";

    /// <summary>A phase stating one fact, one assumption and one decision, as the design
    /// partner's contract now drafts them.</summary>
    public static PhaseDraft Draft(
        string fact = "OrderHandler validates the payload before dispatch",
        string assumption = "the bus client is registered as a singleton",
        string decision = "The sender stays in Api.Orders because OrderHandler.cs:34-41 already owns dispatch.",
        params string[] done) =>
        new(PhaseId, "move the dispatch out of the handler",
            $"""
             phase: {PhaseId}
             goal: "move the dispatch out of the handler"
             decisions:
               - key: |
                   {decision}
             """, [])
        {
            Done = done.Length == 0 ? ["the dispatch no longer runs inside the handler"] : done,
            Facts = fact.Length == 0 ? [] : [new PhaseFact(fact, "[L2] Sample.Server: …")],
            Assumptions = assumption.Length == 0 ? [] : [assumption],
        };

    /// <summary>A draft with no facts, no assumptions and no decisions — every spec the
    /// conversation drafted before the pinned master stated them.</summary>
    public static PhaseDraft DraftWithoutPremises() =>
        new(PhaseId, "move the dispatch out of the handler", $"phase: {PhaseId}", [])
        {
            Done = ["the dispatch no longer runs inside the handler"],
        };

    /// <summary>A phase as the DERIVER renders it: no facts, no assumptions, and the one
    /// decision the framework writes on every derived phase — which names a run artifact that
    /// exists in no sandbox.</summary>
    public static PhaseDraft DerivedDraft() =>
        new(PhaseId, "move the dispatch out of the handler",
            $"""
             phase: {PhaseId}
             goal: "move the dispatch out of the handler"
             decisions:
               - key: |
                   {DerivedPhaseDecision.For("19106", $"{PhaseId}-move-the-dispatch.md")}
             """, [])
        {
            Done = ["the dispatch no longer runs inside the handler"],
        };

    public sealed record Harness(
        CheckPhasePremisesHandler Handler,
        CheckPhasePremisesContext Context,
        PipelineContext Pipeline,
        RecordingEventPublisher Events,
        RecordingTicketComments Ticket,
        RecordingRunDecisions Decisions,
        SearchingProvider Provider)
    {
        public Task<CommandResult> RunAsync() => Handler.ExecuteAsync(Context, CancellationToken.None);

        public string? Verdict => Events.Events
            .OfType<Contracts.Events.PhaseStateChangedEvent>()
            .LastOrDefault(e => e.State == PhaseRunState.HandedBack)?.Verdict;
    }

    /// <summary>The handler with everything real but the model and the sandbox.</summary>
    public static Harness For(
        PhaseDraft draft, string answer, int exitCode = 0,
        params (string Repository, string Pattern)[] searches)
    {
        var provider = new SearchingProvider(answer, searches);
        var pipeline = Pipeline(draft, new CountingSandbox(exitCode));
        var events = EventTestStubs.Recording();
        var ticket = new RecordingTicketComments();
        var decisions = new RecordingRunDecisions();
        var handler = new CheckPhasePremisesHandler(
            new PhasePremiseChecker(
                new CutReviewTestDoubles.CappingFactory(provider),
                new Application.Services.Events.AsyncLocalRunContextAccessor(),
                NullLogger<PhasePremiseChecker>.Instance),
            Factory(),
            new PhaseProgressRecorder(events),
            new PremiseHandbackNotice(
                ticket.Factory(), decisions, NullLogger<PremiseHandbackNotice>.Instance),
            NullLogger<CheckPhasePremisesHandler>.Instance);
        return new Harness(
            handler, new CheckPhasePremisesContext(new AgentConfig(), ticket.Tracker, pipeline),
            pipeline, events, ticket, decisions, provider);
    }

    /// <summary>A comment already on the ticket, as a re-trigger would find it.</summary>
    public static void WithComment(PipelineContext pipeline, string body, bool byUs = true)
    {
        var existing = pipeline.TryGet<IReadOnlyList<AgentSmith.Domain.Entities.TicketComment>>(
            ContextKeys.TicketComments, out var c) && c is not null ? c.ToList() : [];
        existing.Add(new AgentSmith.Domain.Entities.TicketComment(
            byUs ? "agent-smith" : "a person",
            DateTimeOffset.UtcNow.AddMinutes(existing.Count), body));
        pipeline.Set(
            ContextKeys.TicketComments,
            (IReadOnlyList<AgentSmith.Domain.Entities.TicketComment>)existing);
    }

    public static PipelineContext Pipeline(PhaseDraft draft, ISandbox sandbox)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "2026-09-17T09-00-00-0001");
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)
            new Dictionary<string, ISandbox> { [Repo] = sandbox });
        pipeline.Set(ContextKeys.SandboxDiscoveries,
            (IReadOnlyDictionary<string, RemoteContextDiscovery>)
            new Dictionary<string, RemoteContextDiscovery>());
        pipeline.Set(ContextKeys.PhaseSpec, draft);
        pipeline.Set(ContextKeys.Ticket, new AgentSmith.Domain.Entities.Ticket(
            new TicketId("42"), "A ticket", "Its description", null, "Open", "Stub"));
        pipeline.Set(ContextKeys.SpecSet, new SpecSet(
            "azdo-1", [new SpecPhase(draft, draft.PhaseId, string.Empty, [])],
            SpecAccounting.Empty, [], SpecSource.Derived));
        return pipeline;
    }

    /// <summary>Takes the scripted searches one per turn — each naming its own repository, so a
    /// call on a name the look does not carry can be scripted — then answers.</summary>
    public sealed class SearchingProvider(
        string answer, params (string Repository, string Pattern)[] searches) : IChatClient
    {
        private int _turn;

        public List<string> Prompts { get; } = [];
        public List<string> Seen { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            var seen = messages.ToList();
            if (_turn == 0) Prompts.Add(string.Join("\n", seen.Select(m => m.Text)));
            Seen.AddRange(seen.SelectMany(m => m.Contents)
                .OfType<FunctionResultContent>().Select(r => r.Result?.ToString() ?? string.Empty));
            var turn = _turn++;
            if (turn < searches.Length)
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [
                    new FunctionCallContent($"call-{turn}", RepositorySearchTool.Name,
                        new Dictionary<string, object?>
                        {
                            ["repository"] = searches[turn].Repository,
                            ["pattern"] = searches[turn].Pattern,
                        }),
                ])));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        }

        /// <summary>How many times the model was asked — one call per phase, never a second pass.</summary>
        public int Turns => _turn;

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
