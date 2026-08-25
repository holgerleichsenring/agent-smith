namespace AgentSmith.Sandbox.Wire;

public enum StepKind
{
    /// <summary>
    /// 2026-08-25-0d01: a kind this build cannot name — what an older agent sees when a
    /// newer server sends it a step invented after the agent was published. It exists so
    /// the message can be RECEIVED: validation then answers with a result naming the
    /// protocol, which is a report the server can read, instead of a container exit the
    /// server can only read as a death.
    /// </summary>
    Unknown = -1,
    Run = 0,
    Shutdown = 1,
    ReadFile = 2,
    WriteFile = 3,
    ListFiles = 4,
    Grep = 5,
    DirectoryTree = 6
}
