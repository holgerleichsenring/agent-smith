namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// What a booted case needs, and what it deliberately does without.
/// <para>
/// The rig used to hand every case the same absent infrastructure — an address nothing
/// answers and all fifteen hosted services — and every case paid the wait whether its
/// subject was that wait or not. A plan makes each of those a stated choice: the default
/// substitutes the transport and starts nothing, and a case whose subject IS an
/// unreachable dependency says so by naming <see cref="UnreachableRedis"/>.
/// </para>
/// </summary>
public sealed record BootPlan(string ConfigPath)
{
    /// <summary>A loopback port nothing listens on — for the cases that assert on the wait.</summary>
    public const string NothingAnswers = "127.0.0.1:1";

    /// <summary>
    /// The address this case wants its Redis to NOT be at, or null for the in-memory
    /// substitute. Naming one is what keeps a startup-resilience case honest.
    /// </summary>
    public string? UnreachableRedis { get; init; }

    /// <summary>The dashboard API gate, which the server reads from the process.</summary>
    public bool DashboardApi { get; init; } = true;

    /// <summary>
    /// The hosted services this case asserts on. Empty means none: a routing or an
    /// authorization assertion needs no reaper, no poller and no capacity pump.
    /// </summary>
    public IReadOnlyList<Type> HostedServices { get; init; } = [];

    /// <summary>What REDIS_URL is set to for the boot — the dead address either way, so a
    /// substituted case can never reach a Redis that happens to run on the machine.</summary>
    public string RedisUrl => UnreachableRedis ?? NothingAnswers;
}
