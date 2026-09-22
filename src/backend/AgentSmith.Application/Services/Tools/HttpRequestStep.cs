namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-09-22-46ef: the HTTP request as a process the SERVER builds — the program's argument
/// list, or the reason it will not be built.
/// <para>
/// The tool joined and quoted this list into a shell string, which a read-only source scope
/// refused, so a tool the design surface OFFERED never worked there. Sent as a program with
/// its arguments it does, and the request an ordinary sandbox makes is the same either way.
/// </para>
/// <para>
/// Two operands come from the model. curl reads a leading dash as an option, so a URL of
/// <c>-o/path</c> writes a file instead of fetching one — shaping it into an absolute http or
/// https address is what makes it an operand. And curl reads a header beginning with '@' as a
/// FILE to load from disk, which turns a request into an exfiltration of whatever the pod
/// mounts to a host the model picked. The body is safe where it is: <c>--data-raw</c> takes
/// its value literally and never reads a file.
/// </para>
/// </summary>
internal static class HttpRequestStep
{
    public const string Program = "curl";

    /// <summary>The arguments to send, or the refusal to answer with. Never both.</summary>
    public static (string? Refusal, IReadOnlyList<string> Arguments) Build(
        string? method, string? url, string? body, string? headers, int timeoutSeconds)
    {
        string[] methods = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];
        var upper = method?.ToUpperInvariant() ?? "GET";
        if (!methods.Contains(upper))
            return ($"Error: unsupported HTTP method '{method}'. Allowed: "
                    + $"{string.Join(", ", methods)}.", []);
        if (string.IsNullOrWhiteSpace(url)) return ("Error: url is required.", []);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var target)
            || (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps))
            return ($"Error: url must be an absolute http:// or https:// address; "
                    + $"'{url}' is not one.", []);

        var parts = new List<string>
            { "-sS", "-i", "--max-time", timeoutSeconds.ToString(), "-X", upper };
        if (!string.IsNullOrEmpty(headers))
            foreach (var line in headers.Split(
                '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (HeaderRefusal(line) is { } refusal) return (refusal, []);
                parts.Add("-H");
                parts.Add(line);
            }
        if (!string.IsNullOrEmpty(body)) { parts.Add("--data-raw"); parts.Add(body); }
        parts.Add(target.AbsoluteUri);
        return (null, parts);
    }

    private static string? HeaderRefusal(string line) =>
        line.StartsWith('@')
            ? "Error: a request header may not begin with '@' — that asks the transfer program "
              + $"to read a file from disk instead of sending a header. Offending line: '{line}'."
            : line.Contains(':', StringComparison.Ordinal)
                ? null
                : $"Error: each request header must be written as 'Name: value'; '{line}' is not.";
}
