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
/// parks rather than falling through. 2026-11-18-9c1cb: a cited one parks — no second model reads it.
/// </summary>
public sealed class ContradictionHandbackDerivationTests
{
    private const string Ticket = "Send the shipment id with the assets.\n\nThanks.";

    [Fact]
    public async Task SpecSetDeriver_ContradictionWithoutCites_IsReAsked()
    {
        var provider = new StepProvider(Contradiction(cites: null), Cut());

        var (derivation, error) = await Derive(provider);

        error.Should().BeNull();
        derivation!.Set.IsHandedBack.Should().BeFalse("the second answer cut specs");
        provider.Prompts.Should().HaveCount(2);
        provider.Prompts[1].Should().Contain("cites no evidence");
    }

    [Fact]
    public async Task SpecSetDeriver_UncitedOnLastAttempt_ParksUncheckedNotFallback()
    {
        var provider = new StepProvider(
            Contradiction(cites: null), Contradiction(cites: "\"L9\""), Contradiction(cites: null));

        var (derivation, error) = await Derive(provider);

        error.Should().BeNull();
        derivation.Should().NotBeNull("an uncited contradiction parks, never the one-spec fallback");
        derivation!.Set.Handback!.Case.Should().Be(SpecHandbackCase.RequirementsContradictRepository);
        provider.Prompts[2].Should().Contain("L9").And.Contain("names no look");
    }

    [Fact]
    public async Task SpecSetDeriver_UncitedThenUnparseable_StillParksTheHandback()
    {
        var provider = new StepProvider(Contradiction(cites: null), "not json", "not json");

        var (derivation, error) = await Derive(provider);

        error.Should().BeNull("the hand-back is what the run gets, not a rejection to report");
        derivation!.Set.IsHandedBack.Should().BeTrue("the attempts ran out behind it, and it still parks");
    }

    [Fact]
    public async Task SpecSetDeriver_CitedContradiction_ParksWithNoSecondReading()
    {
        var provider = new StepProvider(Search("ShipmentId"), Contradiction(cites: "[\"L1\"]"), Cut());

        var (derivation, error) = await Derive(provider);

        error.Should().BeNull();
        derivation!.Set.Handback!.Cites.Should().ContainSingle().Which.Should().StartWith("[L1] ");
        provider.Prompts.Should().ContainSingle("a cited contradiction parks on the first answer; nothing refutes it");
    }

    private static async Task<(SpecDerivation? Derivation, string? Error)> Derive(
        StepProvider provider)
    {
        var deriver = new SpecSetDeriver(
            new SpecDerivationCall(new CappingFactory(provider), new AsyncLocalRunContextAccessor(), Proof()),
            Factory(), new FixedPrompt(), DerivationTestParsers.Real(), new ScopedContextCoverage(),
            HandbackTestDoubles.Gate(), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), AgentSmith.Tests.TestSupport.ReferenceCarryDoubles.Materializer(), NullLogger<SpecSetDeriver>.Instance);
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

    private sealed class FixedPrompt : IPromptCatalog
    {
        public string Get(string name) => "cut the ticket";

        public string Render(string name, IReadOnlyDictionary<string, string> tokens) => Get(name);
    }
}
