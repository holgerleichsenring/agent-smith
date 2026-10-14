namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-13-8762r: one version of a spec's HTML mock in a design conversation. The number is the
/// rank of the version among the spec's mocks in the conversation, derived at read and never stored;
/// <see cref="Name"/> is derived with it.
/// </summary>
/// <param name="SetId">The version's own set — what its screenshot is stored under.</param>
/// <param name="HasShot">Whether its desktop screenshot is stored; set by a list read only.</param>
public sealed record MockVersion(
    string SetId, string SpecId, int Version, string Name, DateTimeOffset At, bool HasShot = false)
{
    /// <summary>The derived name, <c>{specId}-mock-v{n}</c>; a file adds <c>.html</c> or <c>.jpg</c>.</summary>
    public static string NameOf(string specId, int version) => $"{specId}-mock-v{version}";
}

/// <summary>2026-10-13-8762r: one version's whole page.</summary>
public sealed record MockPage(MockVersion Version, string Html);
