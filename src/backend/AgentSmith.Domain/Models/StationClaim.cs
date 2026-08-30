namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-18e3: what the scan master STATED about one station of one entry group,
/// before anything checked it.
/// <para>
/// A claim is either a location — a <paramref name="File"/> and a <paramref name="StartLine"/>
/// — or an explicit <paramref name="NotLocatedReason"/> naming the input the station would
/// have needed. "This system has no scope station" is a complete answer and is worth more
/// than most findings a scan produces; silence is not an answer, which is why the ABSENCE
/// of a claim and a claim of absence are two different things here.
/// </para>
/// </summary>
public sealed record StationClaim(
    string Group,
    VerificationStation Station,
    string? File,
    int StartLine,
    string? NotLocatedReason);
