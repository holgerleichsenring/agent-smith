namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: a sandbox that can be taken away with NO shutdown grace. An
/// ordinary disposal pushes a shutdown step and then waits a flat ten seconds,
/// serially — thirty seconds for a three-repository conversation, on a capacity door
/// an operator is waiting at, and that door had slow work deliberately taken out of it
/// once already. A held sandbox has no step in flight to finish, so there is nothing
/// for the grace to protect.
/// <para>
/// A backend that cannot remove without the grace does not implement this, and a
/// sandbox that does not implement it cannot be held: the register takes this type, so
/// no hold can exist whose release would wait.
/// </para>
/// </summary>
public interface ISandboxForceRemoval
{
    Task ForceRemoveAsync(CancellationToken cancellationToken);
}
