using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-09-15-ffa7: the cut reviewer as the product composes it — the real reviewer
/// over the real function-invoking client, with a provider that looks as scripted and then
/// answers.</summary>
internal static class CutReviewTestDoubles
{
    public static SpecCutReviewer Reviewer(IChatClientFactory factory) =>
        new(factory, new AsyncLocalRunContextAccessor(), NullLogger<SpecCutReviewer>.Instance);

    public static PipelineCostTracker Tracker() => PipelineCostTracker.GetOrCreate(new PipelineContext());

    public static SpecSet Set(params string[] done) =>
        new("azuredevops-1",
            [new SpecPhase(
                new PhaseDraft("p1a", "migrate the senders", "phase: p1a", []) { Done = done },
                "migrate-the-senders", "# p1a", [])],
            SpecAccounting.Empty, [], SpecSource.Derived);

    /// <summary>Takes the scripted searches one per turn, then answers.</summary>
    public sealed class LookingProvider(string answer, params string[] searches) : IChatClient
    {
        private int _turn;

        public List<string> Prompts { get; } = [];
        public List<AITool> ToolsOffered { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            var seen = messages.ToList();
            if (_turn == 0) Prompts.Add(string.Join("\n", seen.Select(m => m.Text)));
            if (options?.Tools is { } offered && ToolsOffered.Count == 0) ToolsOffered.AddRange(offered);
            var turn = _turn++;
            if (turn < searches.Length)
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [
                    new FunctionCallContent($"call-{turn}", RepositorySearchTool.Name,
                        new Dictionary<string, object?>
                        {
                            ["repository"] = DerivationTestLooks.Repo, ["pattern"] = searches[turn],
                        }),
                ])));
            _turn = 0;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    /// <summary>Wraps the provider in the real function-invoking client at the cap asked for.</summary>
    public sealed class CappingFactory(IChatClient provider) : IChatClientFactory
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
}
