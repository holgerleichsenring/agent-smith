using Microsoft.IdentityModel.Tokens;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-08-25-1806: whether THIS request presented a token the server refused, and which
/// check refused it. A refused token leaves an anonymous principal behind, so without this
/// the identity page showed a caller the server rejected exactly what it shows one it
/// accepted with no roles — and those two have opposite remedies: write a mapping, or fix
/// an audience. The JwtBearer handler is the only thing that knows, and it knows it once,
/// so the answer rides the request it belongs to.
/// </summary>
internal sealed class RefusedToken
{
    private const string ItemKey = "agentsmith.token-refusal";

    /// <summary>Called from the authentication handler's failure event, per request.</summary>
    public void Record(HttpContext context, Exception failure) =>
        context.Items[ItemKey] = Classify(failure);

    /// <summary>Why this request's token was refused, or null when none was refused.</summary>
    public string? Reason(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var reason) ? reason as string : null;

    private static string Classify(Exception failure) => failure switch
    {
        // The handler aggregates when several checks fail at once; the first is the one
        // an operator fixes first, and reporting them all would be reporting the token.
        AggregateException aggregate when aggregate.InnerExceptions.Count > 0
            => Classify(aggregate.InnerExceptions[0]),
        SecurityTokenExpiredException => TokenRefusals.Expired,
        SecurityTokenNotYetValidException => TokenRefusals.NotYetValid,
        SecurityTokenInvalidAudienceException => TokenRefusals.Audience,
        SecurityTokenInvalidIssuerException => TokenRefusals.Issuer,
        SecurityTokenInvalidSignatureException or SecurityTokenSignatureKeyNotFoundException
            => TokenRefusals.Signature,
        SecurityTokenMalformedException => TokenRefusals.Malformed,
        _ => TokenRefusals.Rejected,
    };
}
