namespace AgentSmith.Sandbox.Wire;

public enum StepEventKind
{
    /// <summary>2026-08-25-0d01: an event kind this build cannot name. Carried rather than
    /// thrown on, so one unrecognised line never costs the whole output stream.</summary>
    Unknown = -1,
    Stdout = 0,
    Stderr = 1,
    Started = 2,
    Completed = 3
}
