namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-18-0f27: the concurrent-sandbox bound the Docker capacity probe decided
/// with, together with the place it came from. The source travels WITH the value
/// because a bound that did not take effect is otherwise unanswerable: the catalog,
/// the environment variable and the built-in default all look alike in a log line
/// that prints only a number. Equality is by value, which is what lets the probe
/// report a resolution once and stay quiet until it changes.
/// </summary>
/// <param name="Value">The resolved bound. 0 means unbounded.</param>
/// <param name="Source">Where the value was read from, in operator words.</param>
public sealed record ConcurrentSandboxBound(int Value, string Source);
