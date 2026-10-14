using AgentSmith.Application.Services.Passes;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-10-13-9a59f: the master's open loop and the recorder of its passes.</summary>
public static class MasterPassRegistrations
{
    public static IServiceCollection AddMasterPasses(this IServiceCollection services) =>
        services.AddTransient<MasterPassRecorder>()
            .AddTransient<ReengageStopLog>()
            .AddTransient<ReengagePass>()
            .AddTransient<MasterReengagementLoop>();
}
