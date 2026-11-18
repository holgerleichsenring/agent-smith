namespace DemoSeed;

/// <summary>
/// The time the seeded services see. A conversation turn, a filing and a series record are stamped
/// with "now"; the story sets it before each write so the dashboard shows a plausible afternoon.
/// </summary>
internal sealed class StoryClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => Now;
}
