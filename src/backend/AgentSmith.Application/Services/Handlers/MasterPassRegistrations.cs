using AgentSmith.Application.Services.Passes;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-10-13-9a59f: the master's open loop and the recorder of its passes.</summary>
public static class MasterPassRegistrations
{
    public static IServiceCollection AddMasterPasses(this IServiceCollection services) =>
        services.AddTransient<MasterPassRecorder>()
            .AddTransient<ReengageStopLog>()
            .AddTransient<ReengagePass>().AddTransient<MasterTicketExtras>()
            .AddTransient<MasterReengagementLoop>()
            // 2026-11-22-96c8a: what a spec's coding master leaves once it ended on its report.
            .AddTransient<Specs.Reports.MasterReportReader>().AddTransient<Specs.Reports.MasterReportRecorder>();
}
