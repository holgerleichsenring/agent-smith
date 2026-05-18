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
        var pollingTrackers = config.Trackers.Values.Where(t => t.Polling.Enabled).ToList();
        logger.LogInformation(
            "Configuration: {ProjectCount} project(s), {TrackerCount} tracker(s), {PollingTrackers} polling-enabled tracker(s)",
            config.Projects.Count, config.Trackers.Count, pollingTrackers.Count);

        foreach (var (name, project) in config.Projects)
            LogProject(name, project, logger);

        foreach (var tracker in pollingTrackers)
            LogPollingTracker(config, tracker, logger);
    }

    private static void LogPollingTracker(AgentSmithConfig config, TrackerConnection tracker, ILogger logger)
    {
        var served = config.Projects.Values
            .Where(p => p.Tracker.Name == tracker.Name)
            .Select(p => p.Name)
            .ToList();
        logger.LogInformation(
            "  ⟳ polling tracker '{Tracker}' ({Type}) every {Interval}s — serving projects: [{Projects}]",
            tracker.Name, tracker.Type, tracker.Polling.IntervalSeconds, string.Join(", ", served));
    }

    private static void LogProject(string name, ResolvedProject project, ILogger logger)
    {
        logger.LogInformation(
            "  • {Project}: source={Source}, tickets={Tickets}, agent={Agent}{LegacyPolling}, pipelines=[{Pipelines}]{Triggers}",
            name,
            FormatSource(project.Repo),
            FormatTickets(project.Tracker),
            FormatAgent(project.Agent),
            FormatLegacyPolling(project.Polling),
            FormatPipelines(project),
            FormatTriggers(project));
    }

    private static string FormatLegacyPolling(PollingConfig polling)
        => polling.Enabled ? " [DEPRECATED project-level polling — move to tracker]" : "";

    private static string FormatSource(RepoConnection source) =>
        string.IsNullOrEmpty(source.Url) ? source.Type.ToString() : $"{source.Type} ({source.Url})";

    private static string FormatTickets(TrackerConnection tickets)
    {
        if (tickets.Type == TrackerType.AzureDevOps)
            return $"AzureDevOps ({tickets.Organization}/{tickets.Project})";
        return string.IsNullOrEmpty(tickets.Url) ? tickets.Type.ToString() : $"{tickets.Type} ({tickets.Url})";
    }

    private static string FormatAgent(AgentConfig agent)
    {
        var primary = agent.Models?.Primary?.Model ?? agent.Model;
        return string.IsNullOrEmpty(primary) ? agent.Type : $"{agent.Type} [{primary}]";
    }

    private static string FormatPipelines(ResolvedProject project)
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

    private static string FormatTriggers(ResolvedProject project)
    {
        var triggers = new List<string>();
        if (project.GithubTrigger is not null) triggers.Add("github");
        if (project.GitlabTrigger is not null) triggers.Add("gitlab");
        if (project.AzuredevopsTrigger is not null) triggers.Add("azuredevops");
        if (project.JiraTrigger is not null) triggers.Add("jira");
        return triggers.Count == 0 ? "" : $", triggers=[{string.Join(", ", triggers)}]";
    }
}
