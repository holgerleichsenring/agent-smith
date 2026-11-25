namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-24-f9c5e: the series' own account of its final pull request in one repository — the series branch into the default
/// branch. Never the pull request's state: the beat reads that from the host every time. It keeps what the series opened and
/// the last sentence it said about it, so a beat repeats nothing. 2026-11-25-9992e: the marks the automatic merge and conflict
/// refresh kept went with them; the series master merges and refreshes it.
/// </summary>
public sealed record SeriesDelivery(string Repo, string Url, string Number)
{
    public string? Said { get; init; }
}
