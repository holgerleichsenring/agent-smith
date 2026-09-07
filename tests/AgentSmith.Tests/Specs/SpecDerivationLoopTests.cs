using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-07-b7e2: the tool is not the phase — the LOOP is. The real deriver, the real
/// call and the real function-invoking client over a recording sandbox: a model that
/// keeps looking is refused once the budget is spent, answers in JSON, and the derivation
/// completes with the facts it could cite.
/// </summary>
public sealed class SpecDerivationLoopTests
{
    private const string Ticket = """
        Upgrade the vulnerable packages.

        Thanks.
        """;

    [Fact]
    public async Task ADerivationThatLooksPastItsBudget_IsRefusedInTextAndCompletes()
    {
        var sandbox = new CountingSandbox(exitCode: 1);
        var provider = new LookingChatClient(looks: DerivationLookBudget.Allowance + 1, Reply());

        var (derivation, error) = await Derive(provider, sandbox);

        error.Should().BeNull();
        derivation.Should().NotBeNull("an exhausted budget ends looking, not the derivation");
        sandbox.Ran.Should().HaveCount(DerivationLookBudget.Allowance,
            "the budget is a real fence inside the loop");
        provider.LastMessages
            .SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Select(r => r.Result?.ToString() ?? string.Empty)
            .Should().Contain(t => t == DerivationLookBudget.Exhausted,
                "the refusal reaches the model as a tool result it can decide on");
        var draft = derivation!.Set.Phases[0].Draft;
        draft.Facts.Should().ContainSingle().Which.Evidence.Should().StartWith("[L1] ");
        draft.Assumptions.Should().Equal("the rest are transitive");
    }

    [Fact]
    public async Task ADerivationCall_CarriesTheToolsAndTheIterationCap()
    {
        var sandbox = new CountingSandbox(exitCode: 1);
        var provider = new LookingChatClient(looks: 1, Reply());
        var factory = new CappingFactory(provider);

        await Derive(provider, sandbox, factory);

        factory.Caps.Should().OnlyContain(c => c == SpecDerivationCall.MaxIterations);
        provider.ToolsOffered.Select(t => t.Name).Should().BeEquivalentTo(
            [RepositorySearchTool.Name, RepositoryFileReadTool.Name, DependencyAuditTool.Name]);
    }

    private static async Task<(SpecDerivation? Derivation, string? Error)> Derive(
        LookingChatClient provider, CountingSandbox sandbox, CappingFactory? factory = null)
    {
        var deriver = new SpecSetDeriver(
            new CleanReviewer(),
            new SpecDerivationCall(factory ?? new CappingFactory(provider), new AsyncLocalRunContextAccessor()),
            Factory(),
            new FixedPrompt(),
            DerivationTestParsers.Real(),
            NullLogger<SpecSetDeriver>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes,
            (IReadOnlyDictionary<string, ISandbox>)new Dictionary<string, ISandbox> { [Repo] = sandbox });
        pipeline.Set(ContextKeys.SandboxDiscoveries,
            (IReadOnlyDictionary<string, RemoteContextDiscovery>)new Dictionary<string, RemoteContextDiscovery>());
        var ticket = new Ticket(
            id: new TicketId("19106"), title: "upgrade", description: Ticket,
            acceptanceCriteria: null, status: "open", source: "test");
        return await deriver.DeriveAsync(
            ticket, TicketSegmenter.Segment(Ticket), previous: null, cause: "initial derivation",
            new AgentConfig(), pipeline, CancellationToken.None);
    }

    private static string Reply()
    {
        var segments = TicketSegmenter.Segment(Ticket);
        return $$$"""
            {"phases": [
               {"slug": "raise-the-floors", "goal": "Raise the direct package floors the audit names",
                "steps": [{"id": "raise", "action": "Raise the versions"}],
                "done": ["The manifests carry versions the audit no longer flags."],
                "carries": [{{{segments[0].Id}}}],
                "facts": [{"claim": "no lodash reference exists", "cites": "L1"},
                          {"claim": "the rest are transitive", "cites": ""}] }],
             "discarded": [{"segment": {{{segments[^1].Id}}}, "reason": "a sign-off"}],
             "ignored_instructions": [],
             "handback": {"case": "none", "reason": ""}}
            """;
    }

    /// <summary>Calls the search a fixed number of times, then answers with the cut.</summary>
    private sealed class LookingChatClient(int looks, string answer) : IChatClient
    {
        private int _turn;

        public List<ChatMessage> LastMessages { get; private set; } = [];
        public List<AITool> ToolsOffered { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            LastMessages = [.. messages];
            if (options?.Tools is { } offered && ToolsOffered.Count == 0) ToolsOffered.AddRange(offered);
            var turn = _turn++;
            if (turn < looks)
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [
                    new FunctionCallContent($"call-{turn}", RepositorySearchTool.Name,
                        new Dictionary<string, object?> { ["repository"] = Repo, ["pattern"] = "lodash" }),
                ])));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    /// <summary>Wraps the provider in the real function-invoking client at the cap it was asked for.</summary>
    private sealed class CappingFactory(IChatClient provider) : IChatClientFactory
    {
        public List<int?> Caps { get; } = [];

        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null, MasterLoopHooks? masterLoopHooks = null)
        {
            Caps.Add(maxIterations);
            return provider.AsBuilder()
                .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = maxIterations ?? 25)
                .Build();
        }

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
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
}
