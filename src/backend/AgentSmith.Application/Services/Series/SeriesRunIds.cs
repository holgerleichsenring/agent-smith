namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: a series run's id — random, so any replica mints one without asking another.</summary>
public static class SeriesRunIds
{
    public static string Mint() => "sr-" + Guid.NewGuid().ToString("N")[..12]; // not a limit: the id's width

    /// <summary>2026-11-25-96aea: a series of one's id, derived from its ticket and the ticket's series before it — two openers
    /// racing mint the same id, so the store's unique id lets exactly one of them open it.</summary>
    public static string OneOf(string project, string ticketId, string? before) =>
        "sr-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{project}/{ticketId}/{before}")))[..12].ToLowerInvariant(); // not a limit: the id's width
}
