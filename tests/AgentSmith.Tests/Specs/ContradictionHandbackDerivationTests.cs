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
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.CutReviewTestDoubles;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-12-3a36b: a contradiction hand-back through the real deriver, the real call and the
/// real function-invoking client. An uncited one is asked for again and, on the last attempt,
/// parks unchecked rather than falling through; a cited one is put to the checker with the looks
/// it cites; a refutation buys one more attempt, and a second contradiction is not checked again.
/// </summary>
public sealed class ContradictionHandbackDerivationTests
{
    private const string Ticket = "Send the shipment id with the assets.\n\nThanks.";

    [Fact]
    public async Task SpecSetDeriver_ContradictionWithoutCites_IsReAsked()
    {
        var provider = new StepProvider(Contradiction(cites: null), Cut());
        var checker = new HandbackTestDoubles.StandingChecker();

        var (derivation, error) = await Derive(provider, checker);

        error.Should().BeNull();
        derivation!.Set.IsHandedBack.Should().BeFalse("the second answer cut specs");
        provider.Prompts.Should().HaveCount(2);
        provider.Prompts[1].Should().Contain("cites no evidence");
        checker.Asked.Should().BeEmpty("an uncited contradiction is asked for again, never checked");
    }

    [Fact]
    public async Task SpecSetDeriver_UncitedOnLastAttempt_ParksUncheckedNotFallback()
    {
        var provider = new StepProvider(
            Contradiction(cites: null), Contradiction(cites: "\"L9\""), Contradiction(cites: null));
        var checker = new HandbackTestDoubles.StandingChecker();

        var (derivation, error) = await Derive(provider, checker);

        error.Should().BeNull();
        derivation.Should().NotBeNull("an uncited contradiction parks unchecked, never the one-spec fallback");
        derivation!.Set.Handback!.Case.Should().Be(SpecHandbackCase.RequirementsContradictRepository);
        provider.Prompts[2].Should().Contain("L9").And.Contain("names no look");
        checker.Asked.Should().BeEmpty();
    }

    [Fact]
    public async Task SpecSetDeriver_UncitedThenUnparseable_StillParksTheHandback()
    {
        var provider = new StepProvider(Contradiction(cites: null), "not json", "not json");

        var (derivation, error) = await Derive(provider, new HandbackTestDoubles.StandingChecker());

        error.Should().BeNull("the hand-back is what the run gets, not a rejection to report");
        derivation!.Set.IsHandedBack.Should().BeTrue("the attempts ran out behind it, and it still parks");
    }

    [Fact]
    public async Task SpecSetDeriver_CitedContradiction_IsCheckedWithItsLooksAndParks()
    {
        var provider = new StepProvider(Search("ShipmentId"), Contradiction(cites: "[\"L1\"]"));
        var checker = new HandbackTestDoubles.StandingChecker();

        var (derivation, _) = await Derive(provider, checker);

        derivation!.Set.Handback!.Cites.Should().ContainSingle().Which.Should().StartWith("[L1] ");
        checker.Asked.Should().ContainSingle().Which.Cited.Should().ContainSingle()
            .Which.Id.Should().Be("L1");
    }

    [Fact]
    public async Task SpecSetDeriver_RefutedContradiction_IsAskedAgainWithTheRefutation()
    {
        var provider = new StepProvider(
            Search("ShipmentId"), Contradiction(cites: "L1"), Search("ShipmentId"),
            Contradiction(cites: "L2"), Search("x"), Contradiction(cites: "L3"), Cut());
        var checker = new HandbackTestDoubles.StandingChecker(
            new ContradictionCheck(true, "the request declares ShipmentId", "[C1] Sample.Server: read"));

        var (derivation, _) = await Derive(provider, checker);

        checker.Asked.Should().ContainSingle("a second contradiction after a refutation is not checked again");
        provider.Prompts[1].Should().Contain("refutes it").And.Contain("[C1] Sample.Server: read");
        derivation!.Set.IsHandedBack.Should().BeTrue("the second contradiction parks unchecked");
    }

    [Fact]
    public async Task SpecSetDeriver_RefutedOnTheLastAttempt_GetsOneMoreAttempt()
    {
        var provider = new StepProvider(
            "not json", "not json", Search("ShipmentId"), Contradiction(cites: "L1"), Cut());
        var checker = new HandbackTestDoubles.StandingChecker(
            new ContradictionCheck(true, "the request declares ShipmentId", "[C1] Sample.Server: read"));

        var (derivation, error) = await Derive(provider, checker);

        error.Should().BeNull();
        derivation!.Set.IsHandedBack.Should().BeFalse("the refutation bought a fourth attempt, which cut specs");
    }

    private static async Task<(SpecDerivation? Derivation, string? Error)> Derive(
        StepProvider provider, IContradictionChecker checker)
    {
        var deriver = new SpecSetDeriver(
            new CleanReviewer(),
            new SpecDerivationCall(new CappingFactory(provider), new AsyncLocalRunContextAccessor(), Proof()),
            Factory(), new FixedPrompt(), DerivationTestParsers.Real(), new ScopedContextCoverage(),
            HandbackTestDoubles.Gate(checker), NullLogger<SpecSetDeriver>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)
            new Dictionary<string, ISandbox> { [Repo] = new CountingSandbox(0, "Server/SubmitAssetsRequest.cs:3") });
        pipeline.Set(ContextKeys.SandboxDiscoveries,
            (IReadOnlyDictionary<string, RemoteContextDiscovery>)new Dictionary<string, RemoteContextDiscovery>());
        return await deriver.DeriveAsync(
            new Ticket(new TicketId("1"), "send the id", Ticket, null, "open", "test"),
            TicketSegmenter.Segment(Ticket), previous: null, series: "2026-10-12-0a0a",
            cause: "initial derivation", repositories: [Repo], new AgentConfig(), pipeline, CancellationToken.None);
    }

    private static object Search(string pattern) => new FunctionCallContent(
        $"call-{Guid.NewGuid():N}", RepositorySearchTool.Name,
        new Dictionary<string, object?> { ["repository"] = Repo, ["pattern"] = pattern });

    private static string Contradiction(string? cites) =>
        "{\"specs\": [], \"handback\": {\"case\": \"requirements_contradict_repository\", "
        + "\"reason\": \"SubmitAssetsRequest contains only TruckPlate and TrailerPlate\""
        + (cites is null ? string.Empty : $", \"cites\": {(cites.StartsWith('[') || cites.StartsWith('"') ? cites : $"\"{cites}\"")}")
        + "}}";

    private static string Cut()
    {
        var segments = TicketSegmenter.Segment(Ticket);
        return $$$"""
            {"specs": [
               {"slug": "send-the-id", "goal": "Send the shipment id with the assets",
                "steps": [{"id": "send", "action": "Send the id"}],
                "done": ["The request carries the shipment id."],
                "carries": [{{{segments[0].Id}}}]}],
             "discarded": [{"segment": {{{segments[^1].Id}}}, "reason": "a sign-off"}],
             "handback": {"case": "none", "reason": ""}}
            """;
    }

    /// <summary>Answers each model call with the next scripted step: a tool call or a text.</summary>
    private sealed class StepProvider(params object[] steps) : IChatClient
    {
        private readonly Queue<object> _steps = new(steps);

        /// <summary>The newest user message of every call that answered with text.</summary>
        public List<string> Prompts { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            var next = _steps.Count > 0 ? _steps.Dequeue() : "not json";
            if (next is FunctionCallContent call)
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));
            Prompts.Add(messages.Last(m => m.Role == ChatRole.User).Text);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, (string)next)));
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
            IReadOnlyList<SpecDraft> drafts, string key, string? ticketText, DerivationLook? look,
            AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
            => Task.FromResult(SpecCutReview.Clean);
    }

    private sealed class FixedPrompt : IPromptCatalog
    {
        public string Get(string name) => "cut the ticket";

        public string Render(string name, IReadOnlyDictionary<string, string> tokens) => Get(name);
    }
}
