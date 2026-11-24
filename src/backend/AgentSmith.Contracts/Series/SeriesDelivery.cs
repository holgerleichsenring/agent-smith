namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-24-f9c5e: the series' own account of its final pull request in one repository — the series branch into the default
/// branch. Never the pull request's state: the beat reads that from the host every time. It keeps what the series opened, the
/// last sentence it said about it (so a beat repeats nothing), the head a refresh was launched for (one per head), and when
/// the beat first read no CI on the current head.
/// </summary>
public sealed record SeriesDelivery(string Repo, string Url, string Number)
{
    public string? Said { get; init; }

    public string? RefreshedHead { get; init; }

    public string? NoCiHead { get; init; }

    public DateTimeOffset? NoCiSince { get; init; }
}
