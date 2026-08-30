using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: the form two descriptions of one operation are compared in.
/// <para>
/// A served description writes a path parameter as <c>{id}</c>; a client writes the same
/// call as <c>:orderId</c>, <c>${orderId}</c> or <c>{orderId}</c>. The parameter's NAME is
/// the client's own vocabulary, so it is not part of the identity — everything else is.
/// Without this, every templated path would read as an operation no client calls.
/// </para>
/// </summary>
internal static partial class OperationKey
{
    public static string Of(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation)) return string.Empty;
        var collapsed = Whitespace().Replace(operation.Trim(), " ");
        var templated = Braced().Replace(collapsed, "{}");
        return Colon().Replace(templated, "{}").ToUpperInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\$?\{[^}]*\}|<[^>/\s]+>")]
    private static partial Regex Braced();

    [GeneratedRegex(@"(?<=/):[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex Colon();
}
