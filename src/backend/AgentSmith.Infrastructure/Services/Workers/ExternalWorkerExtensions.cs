using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: registers the external-worker bridge. Registration is not selection — the
/// builder only ever runs for an agent whose configured type is
/// <see cref="ExternalWorkerChatClientBuilder.TypeName"/>.
/// </summary>
public static class ExternalWorkerExtensions
{
    public static IServiceCollection AddExternalWorkerBridge(this IServiceCollection services)
    {
        services.AddSingleton<WorkerJsonFormat>();
        services.AddSingleton<WorkerMessageMapper>();
        services.AddSingleton<WorkerOptionsMapper>();
        services.AddSingleton<WorkerRequestComposer>();
        services.AddSingleton<WorkerPromptRenderer>();
        services.AddSingleton<WorkerReplyParser>();
        services.AddSingleton<WorkerReplyTranslator>();
        services.AddSingleton<ExternalWorkerCliOptionsFactory>();
        // p0419: resilience sits between the bridge and the process, where a dead CLI
        // is still a process concern and not yet an answer.
        services.AddSingleton<AgentCliWorkerProcessRunner>();
        services.AddSingleton<IWorkerProcessRunner>(sp => new RetryingWorkerProcessRunner(
            sp.GetRequiredService<AgentCliWorkerProcessRunner>(),
            sp.GetRequiredService<ILogger<RetryingWorkerProcessRunner>>()));
        services.AddSingleton<IChatClientBuilder, ExternalWorkerChatClientBuilder>();
        return services;
    }
}
