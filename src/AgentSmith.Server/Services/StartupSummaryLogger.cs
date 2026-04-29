using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

/// <summary>
/// Emits a one-shot configuration summary at server startup so operators can
/// see at a glance which projects loaded, their providers, agent types, and
/// whether polling is enabled — without grepping the configmap.
/// </summary>
public static class StartupSummaryLogger
{
    public static void Log(AgentSmithConfig config, ILogger logger)
    {
        var pollingCount = config.Projects.Values.Count(p => p.Polling.Enabled);
        logger.LogInformation(
            "Configuration: {ProjectCount} project(s) loaded, {PollingCount} with polling enabled",
            config.Projects.Count, pollingCount);

        foreach (var (name, project) in config.Projects)
            LogProject(name, project, logger);
    }

    private static void LogProject(string name, ProjectConfig project, ILogger logger)
    {
        logger.LogInformation(
            "  • {Project}: source={Source}, tickets={Tickets}, agent={Agent}, polling={Polling}, pipelines=[{Pipelines}]{Triggers}",
            name,
            FormatSource(project.Source),
            FormatTickets(project.Tickets),
            FormatAgent(project.Agent),
            FormatPolling(project.Polling),
            FormatPipelines(project),
            FormatTriggers(project));
    }

    private static string FormatSource(SourceConfig source) =>
        string.IsNullOrEmpty(source.Url) ? source.Type : $"{source.Type} ({source.Url})";

    private static string FormatTickets(TicketConfig tickets)
    {
        if (string.Equals(tickets.Type, "AzureDevOps", StringComparison.OrdinalIgnoreCase))
            return $"AzureDevOps ({tickets.Organization}/{tickets.Project})";
        return string.IsNullOrEmpty(tickets.Url) ? tickets.Type : $"{tickets.Type} ({tickets.Url})";
    }

    private static string FormatAgent(AgentConfig agent)
    {
        var primary = agent.Models?.Primary?.Model ?? agent.Model;
        return string.IsNullOrEmpty(primary) ? agent.Type : $"{agent.Type} [{primary}]";
    }

    private static string FormatPolling(PollingConfig polling) =>
        polling.Enabled ? $"enabled ({polling.IntervalSeconds}s)" : "disabled";

    private static string FormatPipelines(ProjectConfig project)
    {
        if (project.Pipelines.Count == 0 && string.IsNullOrEmpty(project.Pipeline))
            return "<none>";
        var names = project.Pipelines.Count > 0
            ? string.Join(", ", project.Pipelines.Select(p => p.Name))
            : project.Pipeline;
        return string.IsNullOrEmpty(project.DefaultPipeline)
            ? names
            : $"{names} (default: {project.DefaultPipeline})";
    }

    private static string FormatTriggers(ProjectConfig project)
    {
        var triggers = new List<string>();
        if (project.GithubTrigger is not null) triggers.Add("github");
        if (project.GitlabTrigger is not null) triggers.Add("gitlab");
        if (project.AzuredevopsTrigger is not null) triggers.Add("azuredevops");
        if (project.JiraTrigger is not null) triggers.Add("jira");
        return triggers.Count == 0 ? "" : $", triggers=[{string.Join(", ", triggers)}]";
    }
}
