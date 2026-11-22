using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Specs.Reports;

/// <summary>2026-11-22-96c8a: a chat factory for the report read — keeps the role, messages and options of every call,
/// answers with the given text (ignoring any response format, as Copilot or the worker do) or throws.</summary>
internal sealed class CapturingReadFactory(Func<string> answer) : IChatClientFactory
{
    public List<(TaskType Role, IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, MasterLoopHooks? masterLoopHooks = null) =>
        new Client(this, task);

    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 2048;
    public string GetModel(AgentConfig agent, TaskType task) => "stub-model";

    private sealed class Client(CapturingReadFactory owner, TaskType role) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            owner.Calls.Add((role, [.. messages], options));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, owner.Answer())));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private string Answer() => answer();
}
