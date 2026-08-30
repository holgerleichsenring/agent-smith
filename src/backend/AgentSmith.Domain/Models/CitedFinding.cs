namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-03e1: a finding the scan reports at one station of one entry group, together
/// with the entry of the published standard it names as broken, exactly as stated.
/// <para>
/// The direction of use is the whole point. Nothing hands a station a list to work through
/// any more: the scan investigates, and when it has something to report it looks the clause
/// up and cites it. A finding no entry of the standard covers never comes through here at
/// all — it travels the ordinary observation path unchanged, because three of the five
/// findings this shape was measured against are of exactly that kind.
/// </para>
/// </summary>
public sealed record CitedFinding(
    string Group,
    VerificationStation Station,
    string RequirementId,
    string Level,
    string Text,
    RequirementScope Scope,
    string? File,
    int StartLine,
    IReadOnlyList<string> Members,
    string Detail);
