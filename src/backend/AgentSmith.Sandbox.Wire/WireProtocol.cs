namespace AgentSmith.Sandbox.Wire;

/// <summary>
/// 2026-08-25-0d01: THE ONE PLACE the sandbox wire's protocol version is written down.
/// Every wire record's <c>CurrentSchemaVersion</c> reads <see cref="Current"/>, so the
/// number cannot be raised in one record and forgotten in the other two.
/// <para>
/// OWNER: whoever changes the wire contract, in the same change. This is a repository
/// constant, never configuration — an operator cannot make two builds agree by editing a
/// file, and offering them the knob would only let them declare a compatibility that is
/// not true. Raise <see cref="Current"/> when a message changes shape; raise
/// <see cref="MinimumSupported"/> only when the older shape is genuinely no longer read.
/// </para>
/// <para>
/// The window is deliberately NOT the release version. version.txt, the service config,
/// the compose default, the Kubernetes templates and the harness fixtures name five
/// different releases at any moment — skew between the server and the agent image is the
/// normal state of this product, not a fault. What has to match is the protocol, and two
/// releases that share it can talk however far apart their tags are.
/// </para>
/// </summary>
public static class WireProtocol
{
    /// <summary>The protocol version this build speaks and stamps onto every message it sends.</summary>
    public const int Current = 1;

    /// <summary>The oldest protocol version this build still understands.</summary>
    public const int MinimumSupported = 1;

    /// <summary>True when a message carrying <paramref name="schemaVersion"/> is one this build reads.</summary>
    public static bool IsSupported(int schemaVersion) =>
        schemaVersion >= MinimumSupported && schemaVersion <= Current;

    /// <summary>The window as an operator reads it, e.g. "1" or "1-3".</summary>
    public static string Window =>
        MinimumSupported == Current ? $"{Current}" : $"{MinimumSupported}-{Current}";
}
