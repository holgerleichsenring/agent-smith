using System.Text.Json;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0378: maps one typed run event onto its raw trail row (RunEvents table).
/// Shared by the batching projector and the terminal reconciler so both write
/// the identical row shape.
/// </summary>
public static class RunTrailRowMapper
{
    public static Entities.RunEvent Map(string runId, long seq, RunEvent ev) =>
        new()
        {
            RunId = runId, Seq = seq, Type = ev.Type.ToString(), Timestamp = ev.Timestamp,
            Role = RoleOf(ev), Phase = PhaseOf(ev), Repo = RepoOf(ev),
            PayloadJson = JsonSerializer.Serialize(ev, ev.GetType()),
        };

    private static string? RoleOf(RunEvent ev) => ev switch
    {
        LlmCallFinishedEvent e => e.Role,
        LlmCallStartedEvent e => e.Role,
        ToolCallEvent e => e.Role,
        ToolResultEvent e => e.Role,
        _ => null,
    };

    private static string? PhaseOf(RunEvent ev) => ev switch
    {
        LlmCallFinishedEvent e => e.Phase,
        ToolCallEvent e => e.Phase,
        ToolResultEvent e => e.Phase,
        _ => null,
    };

    private static string? RepoOf(RunEvent ev) => ev switch
    {
        LlmCallFinishedEvent e => e.RepoName,
        ToolCallEvent e => e.RepoName,
        SandboxCreatedEvent e => e.Repo,
        PullRequestOutcomeEvent e => e.Repo,
        _ => null,
    };
}
