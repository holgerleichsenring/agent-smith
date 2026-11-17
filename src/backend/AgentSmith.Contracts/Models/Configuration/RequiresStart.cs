namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-17-9a39a: when a ticket that requires another may start. <see cref="OnGreenPush"/> starts it once
/// every prerequisite it waits for pushed a green branch, stacked on that branch; <see cref="OnMerge"/> waits
/// until each landed, as every project did before.
/// </summary>
public enum RequiresStart
{
    OnGreenPush = 0,
    OnMerge = 1,
}
