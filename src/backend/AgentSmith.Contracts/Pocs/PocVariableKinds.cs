namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: what one entry of a variable write does — set a secret, set a plain value, or delete the
/// name whichever kind it is.</summary>
public static class PocVariableKinds
{
    public const string Secret = "secret";
    public const string Plain = "plain";
    public const string Delete = "delete";
}
