namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0429a: the request a scanner really sent and the response it really got back.
/// <para>
/// A DAST finding's evidence is not a line of source — it is this exchange. The scanners
/// have always had it and the parsers threw it away, which left a live-target claim with
/// nothing behind it but a URL string. A refuter shown a summary is shown a plausible
/// copy of evidence, which is not evidence.
/// </para>
/// </summary>
public sealed record HttpExchange(
    string Method,
    string Url,
    string? Attack = null,
    string? Evidence = null,
    string? Request = null,
    string? Response = null);
