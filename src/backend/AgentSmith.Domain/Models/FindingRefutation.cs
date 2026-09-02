namespace AgentSmith.Domain.Models;

/// <summary>
/// p0429: a fresh instance's attempt to REFUTE one candidate finding against the code it
/// was shown.
/// <para>
/// <see cref="Quote"/> is the anti-invention half, borrowed from the cut review: a
/// rebuttal has to quote a line of the code it was given. A refuter that invents its
/// objection would silence a real critical, which is the one failure this must not
/// introduce — so an unquoted rebuttal is discarded and the finding stands.
/// </para>
/// <para>
/// 2026-09-01-85b2: <see cref="Id"/> is the finding the answer is ABOUT, echoed from the
/// call. Two findings on one line share a location string, so routing by location let one
/// refutation silence both — on the api path, where the location is often just the
/// endpoint, that is the normal case rather than the corner one.
/// </para>
/// </summary>
public sealed record FindingRefutation(
    string Location, bool Substantiated, string? Quote = null, string? Why = null,
    string? Id = null);
