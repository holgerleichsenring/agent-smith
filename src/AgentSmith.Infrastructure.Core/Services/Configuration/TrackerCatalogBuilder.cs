using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Converts raw `trackers:` YAML entries into <see cref="TrackerConnection"/>
/// records keyed by catalog name. Type-parse failures go into the errors list.
/// </summary>
public sealed class TrackerCatalogBuilder
{
    public Dictionary<string, TrackerConnection> Build(
        IReadOnlyDictionary<string, RawTrackerEntry> raw, List<string> errors)
    {
        var result = new Dictionary<string, TrackerConnection>(raw.Count);

        foreach (var (name, entry) in raw)
        {
            var connection = TryBuild(name, entry, errors);
            if (connection is not null) result[name] = connection;
        }
        return result;
    }

    private static TrackerConnection? TryBuild(string name, RawTrackerEntry entry, List<string> errors)
    {
        if (!Enum.TryParse<TrackerType>(entry.Type, ignoreCase: true, out var type))
        {
            errors.Add(
                $"Tracker '{name}': unknown type '{entry.Type}' " +
                "(expected GitHub|GitLab|AzureDevOps|Jira)");
            return null;
        }

        return new TrackerConnection
        {
            Name = name,
            Type = type,
            Url = entry.Url,
            Organization = entry.Organization,
            Project = entry.Project,
            Auth = entry.Auth,
            OpenStates = entry.OpenStates,
            DoneStatus = entry.DoneStatus,
            CloseTransitionName = entry.CloseTransitionName,
            ExtraFields = entry.ExtraFields,
            ZeroMatchComment = entry.ZeroMatchComment,
            Polling = MapPolling(entry.Polling),
        };
    }

    private static PollingConfig MapPolling(RawPollingEntry? raw)
    {
        if (raw is null) return new PollingConfig();
        return new PollingConfig
        {
            Enabled = raw.Enabled,
            IntervalSeconds = raw.IntervalSeconds,
            JitterPercent = raw.JitterPercent,
        };
    }
}
