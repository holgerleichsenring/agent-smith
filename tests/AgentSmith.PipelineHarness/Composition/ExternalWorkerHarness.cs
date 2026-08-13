using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Workers;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0416: points the harness at the external-worker bridge instead of the scripted
/// IChatClientFactory. The PRODUCTION <see cref="ChatClientFactory"/> is restored, so the
/// run resolves the worker exactly as a deployed server would — the only substitution is
/// the subprocess itself, which a scripted runner answers so CI stays deterministic. A
/// live run swaps that one registration back and an agent CLI answers instead.
/// </summary>
public static class ExternalWorkerHarness
{
    public static AgentConfig Agent() => new()
    {
        Type = ExternalWorkerChatClientBuilder.TypeName,
        Model = "harness-worker",
        NetworkTimeoutSeconds = 60,
    };

    public static Action<IServiceCollection> DrivenBy(ScriptedWorkerProcessRunner worker) =>
        services =>
        {
            services.RemoveAll<IChatClientFactory>();
            services.AddSingleton<IChatClientFactory, ChatClientFactory>();
            services.RemoveAll<IWorkerProcessRunner>();
            services.AddSingleton<IWorkerProcessRunner>(worker);
        };
}
