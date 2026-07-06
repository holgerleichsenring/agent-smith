namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Base trigger configuration shared by all webhook platforms.
/// Provides status gating, pipeline-from-label mapping, done_status transition, and comment re-trigger.
/// ProjectResolution is required at the schema level (p0140a); the property is nullable here so
/// the validator can produce a typed error rather than the loader failing with a null-deref.
/// </summary>
public class WebhookTriggerConfig
{
    public ProjectResolutionConfig? ProjectResolution { get; set; }
    public Dictionary<string, string>? PipelineFromLabel { get; set; }
    public string DefaultPipeline { get; set; } = "fix-bug";
    public List<string> TriggerStatuses { get; set; } = [];
    public string DoneStatus { get; set; } = "In Review";

    /// <summary>
    /// p0261: native ticket status a FAILED run moves the ticket to — the failure
    /// counterpart of <see cref="DoneStatus"/>. A processed ticket must never stay
    /// in a trigger status; on failure the ticket is terminalized to this status the
    /// same one-step way success uses done_status. Unset (null) → falls back to
    /// done_status, so the ticket still leaves the open set. What the status is NAMED
    /// is the operator's config concern; it MUST be outside trigger_statuses (config
    /// validation enforces this) or the ticket would be re-claimed immediately.
    /// </summary>
    public string? FailedStatus { get; set; }

    /// <summary>
    /// p0318: native ticket status a run parks the ticket in when the plan needs
    /// clarification (empty body, or the planner returned status=needs_user_input with
    /// open questions). Like <see cref="FailedStatus"/> it MUST be outside
    /// trigger_statuses (config validation enforces this) so discovery does not re-claim
    /// and re-post every poll — the human moving the ticket back to a trigger status
    /// (e.g. "Question" → "To Do") is the natural, visible re-trigger. Unset (null) →
    /// the gate still posts the questions and halts the run, but the ticket is NOT
    /// parked (it stays claimable), so re-posting can recur; set this to park it.
    /// </summary>
    public string? NeedsClarificationStatus { get; set; }

    public string? CommentKeyword { get; set; }
}
