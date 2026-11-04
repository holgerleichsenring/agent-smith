using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// The summarization model and config loader for tests that are NOT about what a model writes outside a run: its
/// model answers with an empty line, which the digest writer discards. Tests that ARE about it build their own.
/// </summary>
internal static class SilentChatModel
{
    internal static IConfigurationLoader Loader(string project)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                [project] = new() { Name = project },
            },
        });
        return loader.Object;
    }

    internal sealed class SilentChatClients : IChatClientFactory, IChatClient
    {
        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null) => this;

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 2048;

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty)));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
