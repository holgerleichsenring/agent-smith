using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Converts raw `trackers:` YAML entries into <see cref="TrackerConnection"/>
/// records keyed by catalog name. Type binding happens at YAML deserialize
/// time via the snake_case enum convention; unknown values fail there.
/// p0515: the catalog is keyed by <see cref="ConfigNames.Comparer"/>. The maps a
/// tracker CARRIES — LifecycleStatusNames, ExtraFields — stay ordinal: they are keyed
/// by provider-defined status and field names, whose case is not ours to normalize.
/// </summary>
public sealed class TrackerCatalogBuilder
{
    private readonly CatalogKeyCollisions _collisions = new();

    public Dictionary<string, TrackerConnection> Build(
        IReadOnlyDictionary<string, RawTrackerEntry> raw, List<StartupFinding> findings)
    {
        var dropped = _collisions.Detect("trackers", raw.Keys, findings);
        var result = new Dictionary<string, TrackerConnection>(raw.Count, ConfigNames.Comparer);

        foreach (var (name, entry) in raw)
        {
            if (dropped.Contains(name)) continue;
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
                NotImplementableStatus = entry.NotImplementableStatus, // p0390
                PipelineFromLabel = entry.PipelineFromLabel,
                ZeroMatchComment = entry.ZeroMatchComment,
                Polling = MapPolling(entry.Polling),
                LifecycleStatusNames = entry.LifecycleStatusNames ?? new Dictionary<string, string>(),
            };
        }

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
