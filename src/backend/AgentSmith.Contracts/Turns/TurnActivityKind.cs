namespace AgentSmith.Contracts.Turns;

/// <summary>
/// What a design turn was doing when it reported. The four kinds are the four things that
/// take time inside a turn: a tool call, a model call, the review of what it drafted, and a
/// re-prompt that sends it back to revise.
/// </summary>
public enum TurnActivityKind
{
    /// <summary>A tool the turn called — a file read, a search, a fetch.</summary>
    Tool,

    /// <summary>A model call that has returned, with the intent it narrated.</summary>
    Model,

    /// <summary>The turn's own proposal is being reviewed against the repositories.</summary>
    Reviewing,

    /// <summary>The turn was sent back over its own answer — a refusal or an invalid outcome.</summary>
    Revising,
}
