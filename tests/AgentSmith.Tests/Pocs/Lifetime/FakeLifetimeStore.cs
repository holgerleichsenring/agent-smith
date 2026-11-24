using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: the PoCs' lifetimes in memory — the subjects the pass and the re-check read, the extensions
/// and seen reminders written.</summary>
internal sealed class FakeLifetimeStore : IPocLifetimeStore
{
    public List<PocLifetimeSubject> Subjects { get; } = [];

    public List<(long PocId, long ExpiresAtTicks)> Extended { get; } = [];

    public List<(long PocId, long SeenAtTicks)> Seen { get; } = [];

    public Task<IReadOnlyList<PocLifetimeSubject>> LiveAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PocLifetimeSubject>>([.. Subjects.Where(s => PocStates.IsLive(s.State))]);

    public Task<PocLifetimeSubject?> ReadAsync(long pocId, CancellationToken ct) => Task.FromResult(Subjects.FirstOrDefault(s => s.PocId == pocId));

    public Task ExtendAsync(long pocId, long expiresAtTicks, CancellationToken ct)
    {
        Extended.Add((pocId, expiresAtTicks));
        var i = Subjects.FindIndex(s => s.PocId == pocId);
        if (i >= 0) Subjects[i] = Subjects[i] with
        {
            Lifetime = Subjects[i].Lifetime with { ExpiresAtTicks = expiresAtTicks, Extensions = Subjects[i].Lifetime.Extensions + 1 },
        };
        return Task.CompletedTask;
    }

    public Task RecordReminderSeenAsync(long pocId, long seenAtTicks, CancellationToken ct)
    {
        Seen.Add((pocId, seenAtTicks));
        return Task.CompletedTask;
    }
}
