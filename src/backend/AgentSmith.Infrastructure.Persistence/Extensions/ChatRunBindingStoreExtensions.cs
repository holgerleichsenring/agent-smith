using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>
/// The relational half of following a chat-started run: the thread bindings and the read of a
/// run's standing. A scoped repository does the work; singleton facades open a scope per op.
/// </summary>
public static class ChatRunBindingStoreExtensions
{
    public static IServiceCollection AddChatRunBindingStore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ChatRunBindingRepository>();
        return services
            .AddSingleton<IChatRunBindingStore, DbChatRunBindingStore>()
            .AddSingleton<IRunOutcomeReader, DbRunOutcomeReader>();
    }
}
