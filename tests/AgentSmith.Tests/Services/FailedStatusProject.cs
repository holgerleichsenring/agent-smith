using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Tests.Services;

/// <summary>2026-11-18-d6d2a: a project whose trigger configures failed_status — the failure path reads it from
/// the project (TicketParkStatuses), no longer from a seeded context key.</summary>
internal static class FailedStatusProject
{
    public static ResolvedProject With(string failedStatus, string name = "", string tracker = "") => new()
    {
        Name = name,
        Tracker = new TrackerConnection { Name = tracker, Type = TrackerType.GitHub },
        GithubTrigger = new WebhookTriggerConfig { FailedStatus = failedStatus },
    };
}
