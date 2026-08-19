namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0327: hybrid-wait tuning for human dialogue, loaded from agentsmith.yml's
/// top-level <c>dialogue:</c> block. Below <see cref="HotWaitSeconds"/> a
/// DialogQuestion is awaited hot in-memory (cheap and latency-free for an
/// operator at the dashboard); past it an eligible ticket run checkpoints,
/// releases its compute, and resumes when the answer arrives.
/// </summary>
public sealed class DialogueGlobalConfig
{
    /// <summary>Hot in-memory wait window in seconds before an eligible run
    /// checkpoints and parks. Default 600 (10 minutes).</summary>
    public int HotWaitSeconds { get; set; } = 600;

    /// <summary>Timeout for the dialogue-routed approval question in seconds.
    /// When it elapses on a checkpointed run, the persisted DefaultAnswer
    /// ("reject") applies headless. Default 259200 (3 days).</summary>
    public int ApprovalTimeoutSeconds { get; set; } = 259_200;

    /// <summary>
    /// p0457: where this deployment's dashboard answers a parked run, e.g.
    /// <c>https://agentsmith.example.com</c>. A ticket comment that asks a question links
    /// to the run's page here so the reader is not left with the tracker as their only
    /// guess. Deliberately without a default: only the deployment knows its own address,
    /// and a guessed localhost printed into someone's work item is a broken link, so unset
    /// means the comment carries no link at all.
    /// </summary>
    public string? DashboardUrl { get; set; }
}
