using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.ChatRuns;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Following a chat-started run back to its thread: the durable binding, the reads of a run's
/// standing and of its current question, the posters by platform, and the watcher that runs
/// on every replica.
/// </summary>
internal static class ChatRunBindingExtensions
{
    internal static IServiceCollection AddChatRunBinding(this IServiceCollection services)
    {
        services.AddChatRunBindingStore();
        services.AddSingleton<IDialogueQuestionReader, RedisDialogueQuestionReader>();
        services.AddTransient<ChatThreadAdapters>();
        services.AddTransient<ChatRunLink>();
        services.AddTransient<ChatRunOutcomeText>();
        services.AddTransient<ChatRunOutcomeNotifier>();
        services.AddTransient<ChatRunQuestionRelay>();
        services.AddTransient<ChatRunFollower>();
        services.AddTransient<ChatRunAnswerRouter>();
        services.AddHostedService<ChatRunBindingWatcher>();
        return services;
    }
}
