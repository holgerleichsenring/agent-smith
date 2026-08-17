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
/// </summary>
public sealed record FindingRefutation(
    string Location, bool Substantiated, string? Quote = null, string? Why = null);
