using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-5701b: the 'file' pipeline's steps and the services they and the server's remaining acts share. Called
/// by the server's composition only: the steps need the tracker existence read and the requirement store a server
/// registers, and the CLI never files an approval.
/// </summary>
public static class FilingPipelineExtensions
{
    public static IServiceCollection AddFilingPipeline(this IServiceCollection services)
    {
        services.TryAddTransient<SpecTicketRenderer>();
        services.TryAddTransient<BugTicketRenderer>();
        services.AddTransient<FiledSeriesFactory>();
        services.AddScoped<ApprovedSpecSetRecorder>().AddScoped<FiledSpecBranchWrite>().AddScoped<ApprovedSetTicketFiler>();
        services.AddScoped<FiledWorkTagger>().AddScoped<FiledWorkMove>().AddScoped<FiledWorkStarter>();
        services.AddScoped<TicketGraphLinks>().AddScoped<TicketGraphStarts>();
        services.AddScoped<SeriesFilingPlan>().AddScoped<ListFilingPlan>().AddScoped<FilingPlanner>();
        services.AddScoped<PlannedTicketFiler>().AddScoped<FilingPlanFiler>();
        services.AddTransient<ICommandHandler<PlanFilingContext>, PlanFilingHandler>();
        services.AddTransient<ICommandHandler<FileTicketsContext>, FileTicketsHandler>();
        return services;
    }
}
