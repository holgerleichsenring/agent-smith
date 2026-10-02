using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-02-5ab2b: the production dispatch over a test's own store and queue double — the
/// request lands on the run row before the queue sees it, exactly as on the server.
/// </summary>
internal static class TestQueuedRunDispatch
{
    internal static QueuedRunDispatch Over(Func<IUnitOfWork> unitOfWork, IRedisJobQueue queue, TimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => unitOfWork());
        services.AddScoped<QueuedRunRepository>();
        return new QueuedRunDispatch(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), queue, clock ?? TimeProvider.System);
    }
}
