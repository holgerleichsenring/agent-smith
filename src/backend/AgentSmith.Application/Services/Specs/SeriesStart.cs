namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-04-6768a: where a series' own work began on one repository's branch, and the base ref
/// that tells work merged in from the base apart from the series' own.
/// </summary>
/// <param name="Commit">The revision before the series' first checkpoint, e.g. <c>{sha}^</c>, or
/// <see cref="Head"/> when the series has committed nothing since its current text was cut.</param>
/// <param name="BaseRef">The remote-tracking base, e.g. <c>origin/main</c>.</param>
public sealed record SeriesStart(string Commit, string BaseRef)
{
    public const string Head = "HEAD";

    /// <summary>True when no series checkpoint lies between the base and HEAD.</summary>
    public bool IsHead => Commit == Head;
}
