using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>2026-11-23-933dc: the chat factory the PoC scan explanation is given — it records each prompt and answers
/// <see cref="Answer"/>, or throws when <see cref="Fails"/>.</summary>
internal sealed class ExplainChats : IChatClientFactory, IChatClient
{
    internal List<string> Prompts { get; } = [];

    internal string Answer { get; set; } = "Remove the key and upload again.";

    internal bool Fails { get; set; }

    public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, MasterLoopHooks? masterLoopHooks = null) => this;

    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 2048;

    public string GetModel(AgentConfig agent, TaskType task) => "stub-model";

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Prompts.Add(string.Join("\n", messages.Select(m => m.Text)));
        if (Fails) throw new HttpRequestException("the model is unreachable");
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
