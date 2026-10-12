using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Application.Services.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: reads a registry's <c>WWW-Authenticate: Bearer realm="…",service="…",
/// scope="…"</c> challenge. A quoted value may hold commas (a scope naming two actions), so
/// the fields are read quote by quote, never split on commas.
/// </summary>
public sealed class RegistryChallengeParser
{
    private const string Scheme = "Bearer ";

    /// <summary>Null when the header is absent, not a Bearer challenge, or names no usable realm.</summary>
    public RegistryChallenge? Parse(string? header)
    {
        if (header is null || !header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)) return null;
        var fields = Fields(header[Scheme.Length..]);
        if (!fields.TryGetValue("realm", out var realm)
            || !Uri.TryCreate(realm, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http"))
            return null;
        return new RegistryChallenge(uri, fields.GetValueOrDefault("service"), fields.GetValueOrDefault("scope"));
    }

    private static Dictionary<string, string> Fields(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var position = 0;
        while (position < text.Length)
        {
            var equals = text.IndexOf('=', position);
            if (equals < 0) break;
            var key = text[position..equals].Trim(' ', ',');
            var (value, next) = Value(text, equals + 1);
            fields[key] = value;
            position = next;
        }
        return fields;
    }

    private static (string Value, int Next) Value(string text, int start)
    {
        if (start < text.Length && text[start] == '"')
        {
            var close = text.IndexOf('"', start + 1);
            return close < 0 ? (text[(start + 1)..], text.Length) : (text[(start + 1)..close], close + 1);
        }
        var comma = text.IndexOf(',', start);
        return comma < 0 ? (text[start..].Trim(), text.Length) : (text[start..comma].Trim(), comma + 1);
    }
}
