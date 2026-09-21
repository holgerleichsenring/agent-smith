using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-09-20-4b0af: the conversation-subject minter for tests that are NOT about minting.
/// Its model answers with an empty line, which the admission discards, so the conversation is
/// left exactly as it was before the mint existed — no stored subject, and no swallowed
/// exception standing in for one. Tests that ARE about minting build their own.
/// </summary>
internal static class SilentSubjectMinter
{
    internal static SpecDialogSubjectMinter Over(
        SpecDialogSessionRepository repository, string project = "sample") =>
        new(new SilentChatClients(), Loader(project), new ServerContext("agentsmith.yml"),
            repository, NullLogger<SpecDialogSubjectMinter>.Instance);

    private static IConfigurationLoader Loader(string project)
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

    private sealed class SilentChatClients : IChatClientFactory, IChatClient
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
