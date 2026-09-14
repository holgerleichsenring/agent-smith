namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-08-25-1806: the closed vocabulary a refused token is described by. A CLASSIFICATION
/// rather than the validation message: the message a token-validation failure carries names
/// the values the check ran against, and the identity page is reachable by anyone who can
/// present a token this server did not accept. Which check failed is enough to act on and
/// says nothing the caller did not already supply; the detail stays in the server's log.
/// <para>
/// 2026-09-14-c72e amended the second half of that, not the first. "Enough to act on" was
/// measured and found wanting: a refusal named 'audience' sent an operator through a session
/// of decoding tokens by hand to learn which audience had arrived. So the three fields that
/// say WHICH SHAPE arrived travel beside this classification — see <see cref="PresentedToken"/>
/// for why that is still nothing the caller did not supply. The library's MESSAGE stays out,
/// which is what this vocabulary exists for: it is a contract, and the message is not.
/// </para>
/// </summary>
internal static class TokenRefusals
{
    internal const string Expired = "expired";
    internal const string NotYetValid = "not_yet_valid";
    internal const string Audience = "audience";
    internal const string Issuer = "issuer";
    internal const string Signature = "signature";
    internal const string Malformed = "malformed";

    /// <summary>The token was refused and none of the named checks is why.</summary>
    internal const string Rejected = "rejected";
}
