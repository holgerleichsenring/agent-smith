using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Converts raw `trackers:` YAML entries into <see cref="TrackerConnection"/>
/// records keyed by catalog name. Type binding happens at YAML deserialize
/// time via the snake_case enum convention; unknown values fail there.
/// </summary>
public sealed class TrackerCatalogBuilder
{
    public Dictionary<string, TrackerConnection> Build(
        IReadOnlyDictionary<string, RawTrackerEntry> raw, List<string> _)
    {
        var result = new Dictionary<string, TrackerConnection>(raw.Count);

        foreach (var (name, entry) in raw)
            result[name] = new TrackerConnection
            {
                Name = name,
                Type = entry.Type,
                Url = entry.Url,
                Organization = entry.Organization,
                Project = entry.Project,
                Auth = entry.Auth,
                OpenStates = entry.OpenStates,
                DoneStatus = entry.DoneStatus,
                CloseTransitionName = entry.CloseTransitionName,
                ExtraFields = entry.ExtraFields,
                TriggerStatuses = entry.TriggerStatuses,
                FailedStatus = entry.FailedStatus,
                NeedsClarificationStatus = entry.NeedsClarificationStatus,
                PipelineFromLabel = entry.PipelineFromLabel,
                ZeroMatchComment = entry.ZeroMatchComment,
                Polling = MapPolling(entry.Polling),
                LifecycleStatusNames = entry.LifecycleStatusNames ?? new Dictionary<string, string>(),
            };

        return result;
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
