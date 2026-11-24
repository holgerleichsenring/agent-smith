namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-17-f3a8d: what a secret run does — set a value, delete one, or re-wrap every stored value under the
/// current master key — for the person who asked. <paramref name="Value"/> only on a set. 2026-11-23-933dg:
/// <paramref name="Scope"/> names whose value it is; none is the installation's.
/// </summary>
public sealed record SecretWriteRequest(string Name, string Operation, SecretInput? Value, string Person, SecretScope? Scope = null)
{
    /// <summary>The scope written; the installation when the request names none.</summary>
    public SecretScope In => Scope ?? SecretScope.Installation;

    public const string Set = "set";
    public const string Delete = "delete";
    public const string Rewrap = "rewrap";
}
