using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.Browser;

/// <summary>2026-10-13-8762r: both mock ports over a list — versions numbered per spec in write order.</summary>
internal sealed class MemoryMocks : IConversationMocks, IConversationMockReader
{
    public List<(string Session, string SetId, string SpecId, string Html)> Pages { get; } = [];
    public Dictionary<string, byte[]> Shots { get; } = [];

    public Task<(MockVersion? Version, string? Refusal)> AddAsync(string sessionId, string specId, string html, CancellationToken cancellationToken)
    {
        var setId = $"mock-{Pages.Count + 1}";
        Pages.Add((sessionId, setId, specId, html));
        return Task.FromResult<(MockVersion?, string?)>((Of(setId), null));
    }

    public Task<bool> SetShotAsync(string sessionId, string setId, byte[] jpeg, CancellationToken cancellationToken)
    {
        Shots[setId] = jpeg;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<MockVersion>> ListAsync(string sessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MockVersion>>([.. Pages.Where(p => p.Session == sessionId).Select(p => Of(p.SetId))]);

    public Task<MockPage?> HtmlAsync(string sessionId, string specId, int? version, CancellationToken cancellationToken)
    {
        var held = Pages.Where(p => p.Session == sessionId && p.SpecId == specId).ToList();
        var n = version ?? held.Count;
        return Task.FromResult(n >= 1 && n <= held.Count ? new MockPage(Of(held[n - 1].SetId), held[n - 1].Html) : null);
    }

    public Task<byte[]?> ShotAsync(string sessionId, string specId, int version, CancellationToken cancellationToken) =>
        Task.FromResult(Pages.Where(p => p.SpecId == specId).ElementAtOrDefault(version - 1) is { SetId: { } set }
            ? Shots.GetValueOrDefault(set) : null);

    private MockVersion Of(string setId)
    {
        var page = Pages.Single(p => p.SetId == setId);
        var n = Pages.Where(p => p.SpecId == page.SpecId).ToList().FindIndex(p => p.SetId == setId) + 1;
        return new MockVersion(setId, page.SpecId, n, MockVersion.NameOf(page.SpecId, n), DateTimeOffset.UnixEpoch, Shots.ContainsKey(setId));
    }
}
