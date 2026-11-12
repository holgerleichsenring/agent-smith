using AgentSmith.Application.Services.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>2026-11-12-cd4ca: the design master's write checks and the tools over them.</summary>
public static class ProposalWriteRegistrations
{
    public static IServiceCollection AddProposalWrites(this IServiceCollection services) =>
        services.AddTransient<TicketDraftWrite>().AddTransient<SpecDraftWrite>().AddTransient<SpecDraftIds>()
            .AddTransient<SpecDraftShape>().AddTransient<ContractDraftWrite>().AddTransient<ProposalWriteTools>();
}
