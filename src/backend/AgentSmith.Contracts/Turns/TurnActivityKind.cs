namespace AgentSmith.Contracts.Turns;

/// <summary>
/// What a design turn was doing when it reported: a tool call, a model call, or a re-prompt that
/// sends it back to revise. 2026-11-18-9c1ca: no review runs after the turn, so there is no Reviewing.
/// </summary>
public enum TurnActivityKind
{
    /// <summary>A tool the turn called — a file read, a search, a fetch.</summary>
    Tool,

    /// <summary>A model call that has returned, with the intent it narrated.</summary>
    Model,

    /// <summary>The turn was sent back over its own answer — a refusal or an invalid outcome.</summary>
    Revising,

    /// <summary>2026-11-19-687ea: a model call has started and not returned yet — the page shows it as a live line.</summary>
    Thinking,

    /// <summary>2026-11-20-24bd: a model call is waiting for its run's place on the deployment (agent.max_concurrent_runs);
    /// the detail is the sentence the page shows.</summary>
    Waiting,
}
