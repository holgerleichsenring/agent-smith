namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-17-f3a8d: what a secret run does — set a value, delete one, or re-wrap every stored value under the
/// current master key — for the person who asked. <paramref name="Value"/> only on a set.
/// </summary>
public sealed record SecretWriteRequest(string Name, string Operation, SecretInput? Value, string Person)
{
    public const string Set = "set";
    public const string Delete = "delete";
    public const string Rewrap = "rewrap";
}
