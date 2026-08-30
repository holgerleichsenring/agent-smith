namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-18e3: one station of one entry group AFTER the run checked the claim.
/// <para>
/// <paramref name="Located"/> is not the master's word for it. A station counts as located
/// only when it names a file the scan really read and a line inside it — the same rule that
/// decides whether a finding may call itself analyzed-from-source, so a located station
/// carries exactly as much weight as a delivered finding and no more. A location that
/// resolves against nothing is not a location, and <paramref name="Note"/> says which case
/// it is: the master declined the station itself, it named a file nothing in this run
/// opened, or it named no line at all.
/// </para>
/// </summary>
public sealed record StationLocation(
    VerificationStation Station,
    string? File,
    int StartLine,
    bool Located,
    string Note)
{
    /// <summary>The location as a reader reads it, or the empty string when there is none.</summary>
    public string Display =>
        Located && !string.IsNullOrWhiteSpace(File) ? $"{File}:{StartLine}" : string.Empty;
}
