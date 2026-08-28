using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-25-7035: the factory the account eval runs on — one client, wrapped in the tool
/// loop for the task the account actually asks for.
/// <para>
/// Production treats Reasoning as tool-bearing, which is the only reason the account can
/// search at all. A factory that handed back a bare client would score an account with no
/// tool — a different component, scoring well on the criteria that need looking least.
/// </para>
/// </summary>
internal sealed class AccountEvalChatFactory(IChatClient client, string modelId) : IChatClientFactory
{
    public IChatClient Create(
        AgentConfig agent, TaskType task, int? maxIterations = null,
        MasterLoopHooks? masterLoopHooks = null) =>
        task is TaskType.Primary or TaskType.Scout or TaskType.Planning or TaskType.Reasoning
            ? new ChatClientBuilder(client)
                .UseFunctionInvocation(configure: c =>
                    c.MaximumIterationsPerRequest = maxIterations ?? 25)
                .Build()
            : client;

    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;

    public string GetModel(AgentConfig agent, TaskType task) => modelId;
}

/// <summary>
/// 2026-08-25-7035: an IChatClient that answers from the prompt it is given, for the tier
/// that must prove the arithmetic without paying for a model.
/// </summary>
internal sealed class PromptScriptedChatClient(Func<string, string> answer) : IChatClient
{
    private readonly List<string> _offeredTools = [];
    private readonly List<string> _prompts = [];

    public int InvocationCount { get; private set; }

    /// <summary>2026-08-28-c310: every tool name this client was offered, so a test can assert
    /// what the account under test could actually call rather than what it answered.</summary>
    public IReadOnlyList<string> OfferedTools => _offeredTools;

    /// <summary>What the account was shown, for a test that asserts the evidence reached it.</summary>
    public IReadOnlyList<string> Prompts => _prompts;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        var prompt = string.Join("\n", messages.Select(m => m.Text));
        _prompts.Add(prompt);
        foreach (var tool in options?.Tools ?? [])
            if (!_offeredTools.Contains(tool.Name, StringComparer.Ordinal))
                _offeredTools.Add(tool.Name);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer(prompt))));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
