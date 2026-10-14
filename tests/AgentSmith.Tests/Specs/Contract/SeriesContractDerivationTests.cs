using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Prompts;
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
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.CutReviewTestDoubles;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs.Contract;

/// <summary>
/// 2026-10-13-8762j: a derivation reply's contract is checked like any other reply defect and sent back with
/// the reason — except on the last attempt, where the cut is kept without it; a re-cut repeats the previous
/// contract, and a hand-back is never asked for one.
/// </summary>
public sealed class SeriesContractDerivationTests
{
    private const string Ticket = "Upgrade the vulnerable packages.\n\nThanks.";
    private const string Series = "2026-10-14-0c0c";
    private const string Colliding = """{"data": [{"datum": "rounds", "lives_in": "R", "owner": 1}, {"datum": "rounds", "lives_in": "R", "owner": 2}]}""";
    private const string Clean = """{"names": [{"name": "SpecReviewRound", "kind": "event", "number": 87, "owner": 1, "writers": [2]}]}""";

    [Fact]
    public void SpecDerivationParser_CollidingContract_IsRejectedWithTheReason()
    {
        var parsed = Parse(Cut(Colliding));

        parsed.Derivation!.Set.Contract.Should().BeNull("the set beside a refused contract is built without one");
        parsed.ContractProblem.Should().Contain("datum 'rounds' is claimed by 2 owner entries");
    }

    [Fact]
    public void SpecDerivationParser_NoContract_ParsesAsBefore()
    {
        var parsed = Parse(Cut(null));

        parsed.Error.Should().BeNull();
        parsed.ContractProblem.Should().BeNull();
        parsed.Derivation!.Set.Contract.Should().BeNull();
        parsed.Derivation.Set.Specs.Select(s => s.SpecId).Should().Equal(Series + "a", Series + "b");
    }

    [Fact]
    public void SpecDerivationParser_CleanContract_RidesOnTheSet()
    {
        var name = Parse(Cut(Clean)).Derivation!.Set.Contract!.Names.Should().ContainSingle().Subject;

        (name.Name, name.Number, name.Owner).Should().Be(("SpecReviewRound", 87, Series + "a"));
        name.Writers.Should().Equal(Series + "b");
    }

    [Fact]
    public async Task SpecSetDeriver_CollidingThenClean_SendsTheReasonBack()
    {
        var provider = new StepProvider(Cut(Colliding), Cut(Clean));

        var (derivation, _) = await Derive(provider, previous: null);

        provider.Prompts[1].Should().Contain("Your series contract was rejected").And.Contain("claimed by 2 owner entries");
        derivation!.Set.Contract.Should().NotBeNull();
    }

    [Fact]
    public async Task SpecSetDeriver_ContractDefectOnLastAttempt_KeepsTheCutWithoutContract()
    {
        var provider = new StepProvider(Cut(Colliding), Cut(Colliding), Cut(Colliding));

        var (derivation, error) = await Derive(provider, previous: null);

        error.Should().BeNull();
        provider.Prompts.Should().HaveCount(3);
        derivation!.Set.Specs.Should().HaveCount(2, "an optional block must not cost a valid cut");
        derivation.Set.Contract.Should().BeNull();
    }

    [Fact]
    public async Task SpecSetDeriver_ReplyDropsPreviousContract_IsRejectedWithTheReason()
    {
        var provider = new StepProvider(Cut(null), Cut(Clean));

        var (derivation, _) = await Derive(provider, Previous());

        provider.Prompts[1].Should().Contain(DerivedContractRule.Dropped);
        derivation!.Set.Contract.Should().NotBeNull();
    }

    [Fact]
    public async Task SpecSetDeriver_HandbackOnRecutWithPreviousContract_IsNotRejected()
    {
        var provider = new StepProvider("""
            {"specs": [], "handback": {"case": "question", "reason": "the ticket reads two ways",
             "readings": ["upgrade the direct packages", "upgrade every package"], "taken": 0}}
            """);

        var (derivation, _) = await Derive(provider, Previous());

        provider.Prompts.Should().ContainSingle("a hand-back carries no specs and so no contract");
        derivation!.Set.IsHandedBack.Should().BeTrue();
    }

    [Fact]
    public void PreviousCutPromptSection_PreviousContract_IsRendered()
    {
        var section = PreviousCutPromptSection.Render(Previous(), SpecRevisionCause.Retrigger, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults);

        section.Should().Contain("## The series contract")
            .And.Contain($"- name: event 'SpecReviewRound' = 87, owner {Series}a, writers {Series}b");
    }

    [Fact]
    public void PreviousCutPromptSection_PreviousContract_SaysMembersByPosition()
    {
        PreviousCutPromptSection.Render(Previous(), SpecRevisionCause.Retrigger, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults)
            .Should().Contain("name members by their position in your reply; only executed specs keep their ids");
        PreviousCutPromptSection.Render(Previous() with { Contract = null }, SpecRevisionCause.Retrigger, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults)
            .Should().NotContain("series contract");
    }

    private static SpecDerivationParser.Parsed Parse(string reply) => DerivationTestParsers.Real().Parse(
        reply, "azdo-1", Series, "1", TicketSegmenter.Segment(Ticket), SpecSource.Derived);

    private static async Task<(SpecDerivation? Derivation, string? Error)> Derive(StepProvider provider, SpecSet? previous)
    {
        var deriver = new SpecSetDeriver(
            AgentSmith.Tests.TestSupport.CutReviewSteps.Of(new CleanReviewer()),
            new SpecDerivationCall(new CappingFactory(provider), new AsyncLocalRunContextAccessor(), Proof()),
            Factory(), new FixedPrompt(), DerivationTestParsers.Real(), new ScopedContextCoverage(),
            AgentSmith.Tests.TestSupport.HandbackTestDoubles.Gate(), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSetDeriver>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)
            new Dictionary<string, ISandbox> { [Repo] = new CountingSandbox(0) });
        pipeline.Set(ContextKeys.SandboxDiscoveries,
            (IReadOnlyDictionary<string, RemoteContextDiscovery>)new Dictionary<string, RemoteContextDiscovery>());
        return await deriver.DeriveAsync(
            new Ticket(new TicketId("1"), "upgrade", Ticket, null, "open", "test"), TicketSegmenter.Segment(Ticket),
            previous, Series, previous is null ? "initial derivation" : SpecRevisionCause.Retrigger, [], new AgentConfig(),
            pipeline, CancellationToken.None);
    }

    private static SpecSet Previous() => new(
        "azdo-1", [Spec(Series + "a", "Raise the direct floors"), Spec(Series + "b", "Raise the rest")],
        SpecAccounting.Empty, [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.BranchArtifact, Series: Series)
    {
        Contract = SeriesContractFixtures.Of(names: [new("SpecReviewRound", "event", 87, Series + "a", [Series + "b"])]),
    };

    private static DerivedSpec Spec(string id, string goal) => new(
        new SpecDraft(id, goal, $"spec: {id}\ngoal: \"{goal}\"", []) { Done = [$"{goal} is done."] }, id, string.Empty, []);

    private static string Cut(string? contract)
    {
        var segments = TicketSegmenter.Segment(Ticket);
        return $$$"""
            {"specs": [
               {"slug": "raise-the-direct-floors", "goal": "Raise the direct package floors",
                "steps": [{"id": "raise", "action": "Raise the versions"}],
                "done": ["The manifests carry versions the audit no longer flags."],
                "carries": [{{{segments[0].Id}}}]},
               {"slug": "raise-the-rest", "goal": "Raise the transitive floors",
                "steps": [{"id": "raise", "action": "Raise the lock file"}],
                "done": ["The lock file carries versions the audit no longer flags."]}],
             "discarded": [{"segment": {{{segments[^1].Id}}}, "reason": "a sign-off"}],
             {{{(contract is null ? string.Empty : $"\"contract\": {contract},")}}}
             "handback": {"case": "none", "reason": ""}}
            """;
    }

    /// <summary>Answers each model call with the next scripted text and records the newest user message.</summary>
    private sealed class StepProvider(params string[] steps) : IChatClient
    {
        private readonly Queue<string> _steps = new(steps);

        public List<string> Prompts { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            Prompts.Add(messages.Last(m => m.Role == ChatRole.User).Text);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _steps.Count > 0 ? _steps.Dequeue() : "not json")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class CleanReviewer : ISpecCutReviewer
    {
        public Task<SpecCutReview> ReviewAsync(
            IReadOnlyList<SpecDraft> drafts, string key, string? ticketText, DerivationLook? look,
            AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken) =>
            Task.FromResult(SpecCutReview.Clean);
    }

    private sealed class FixedPrompt : IPromptCatalog
    {
        public string Get(string name) => "cut the ticket";

        public string Render(string name, IReadOnlyDictionary<string, string> tokens) => Get(name);
    }
}
