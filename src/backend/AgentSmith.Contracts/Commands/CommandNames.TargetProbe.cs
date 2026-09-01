namespace AgentSmith.Contracts.Commands;

/// <summary>
/// 2026-09-01-379a: the step that asks the target environment whether it answers.
/// <para>
/// Its own partial file rather than a row in CommandNames.Pipeline, which is at its
/// length baseline: a file already over the limit may only get shorter, and a step
/// whose whole subject is "does the estate outside this run answer?" reads as a
/// heading of its own anyway.
/// </para>
/// </summary>
public static partial class CommandNames
{
    /// <summary>2026-09-01-379a: runs each context's declared <c>probe:</c> command through
    /// a shell, after EnsurePrerequisites (which installs the CLI the probe calls) and
    /// before the master, so a wrong or absent credential costs no model token. A refusal
    /// fails the run naming the target, the command and the exit code, and carries NO
    /// captured output — the masker only knows values the framework holds, and by design it
    /// never holds an injected credential. Three outcomes are distinguishable in the record:
    /// answered, refused, and not declared.</summary>
    public const string ProbeTarget = "ProbeTargetCommand";
}
