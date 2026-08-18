using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
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
/// p0447: a cut the reviewer still objects to after the last attempt is worth more than
/// no cut at all.
/// <para>
/// The deriver retries three times, hands the objections back each time, and then throws
/// the cut away. What replaces it is one phase carrying the whole ticket — no boundaries,
/// no per-phase verdict, no repair pass, and on live run 2552 nineteen minutes of work
/// with no way to end. Four phases with an objection against the fourth are strictly
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
            "two phases with one objection beat one phase carrying the whole ticket");
        derivation!.Set.Phases.Should().HaveCount(2);
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

    private static async Task<(SpecDerivation? Derivation, string? Error)> Derive(
        string reply, SpecCutReview verdict)
    {
        var deriver = new SpecSetDeriver(
            new FixedReviewer(verdict),
            new SingleClientFactory(new AlwaysAnswers(reply)),
            new FixedPrompt(),
            new SpecDerivationParser(
                new SpecDraftValidator(new PhaseSpecSchemaProvider()), new PhaseDraftReader(),
                new DerivedPhaseYamlRenderer(), new SpecDerivationEnvelope()),
            new AsyncLocalRunContextAccessor(),
            NullLogger<SpecSetDeriver>.Instance);

        var ticket = new Ticket(
            id: new TicketId("19106"), title: "migrate", description: Ticket,
            acceptanceCriteria: null, status: "open", source: "test");
        return await deriver.DeriveAsync(
            ticket, TicketSegmenter.Segment(Ticket), previous: null, cause: "initial derivation",
            new AgentConfig(), new PipelineContext(), CancellationToken.None);
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
            {"phases": [
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
            SpecSet set, string ticketText, AgentConfig agent,
            PipelineCostTracker costTracker, CancellationToken cancellationToken)
            => Task.FromResult(verdict);
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
