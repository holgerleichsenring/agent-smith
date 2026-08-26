namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-08-25-1806: the closed vocabulary a refused token is described by. A CLASSIFICATION
/// rather than the validation message: the message a token-validation failure carries names
/// the values the check ran against, and the identity page is reachable by anyone who can
/// present a token this server did not accept. Which check failed is enough to act on and
/// says nothing the caller did not already supply; the detail stays in the server's log.
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
