namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-13-8762r: the WRITER of a design conversation's HTML mocks — each write is the spec's next
/// version, kept with its desktop screenshot once rendered. Absent where there is no store. Reads go
/// through <see cref="IConversationMockReader"/> only.
/// </summary>
public interface IConversationMocks
{
    /// <summary>The longest mock kept, below the tool-result bound so it reads back whole.</summary>
    public const int MaxChars = 90_000;

    /// <summary>The most versions one spec keeps in a conversation.</summary>
    public const int MaxVersionsPerSpec = 20;

    /// <summary>The most versions a conversation keeps over all its specs.</summary>
    public const int MaxVersionsPerConversation = 100;

    /// <summary>The stored version, or the refusal naming the cap it would break.</summary>
    Task<(MockVersion? Version, string? Refusal)> AddAsync(
        string sessionId, string specId, string html, CancellationToken cancellationToken);

    /// <summary>Stores the screenshot of the version in <paramref name="setId"/>; false when no such version is held.</summary>
    Task<bool> SetShotAsync(string sessionId, string setId, byte[] jpeg, CancellationToken cancellationToken);
}

/// <summary>
/// 2026-10-13-8762r: the ONE reader of a conversation's mocks. A version is addressed as (spec id,
/// version), which only this reader resolves to its set.
/// </summary>
public interface IConversationMockReader
{
    /// <summary>Every version of every spec, oldest first, without any bytes.</summary>
    Task<IReadOnlyList<MockVersion>> ListAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>One version's page — the newest when <paramref name="version"/> is null — or null.</summary>
    Task<MockPage?> HtmlAsync(string sessionId, string specId, int? version, CancellationToken cancellationToken);

    /// <summary>One version's screenshot bytes, or null when it has none or is not held.</summary>
    Task<byte[]?> ShotAsync(string sessionId, string specId, int version, CancellationToken cancellationToken);
}
