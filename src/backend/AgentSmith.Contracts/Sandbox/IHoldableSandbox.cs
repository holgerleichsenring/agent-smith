namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: a sandbox a design conversation may hold between turns. It is both
/// halves at once: a later turn READS through it, and a capacity door may take it away
/// with no shutdown grace.
/// <para>
/// Neither half alone would do. A hold that could only be removed by an ordinary disposal
/// would make a door wait ten seconds per sandbox (2026-09-22-2d11a), and a hold that
/// could only be removed could not be reused, which is the whole point of holding one.
/// A backend that offers only one of the two cannot be held: its scope disposes at the
/// end of the turn exactly as it always did.
/// </para>
/// </summary>
public interface IHoldableSandbox : ISandbox, ISandboxForceRemoval;
