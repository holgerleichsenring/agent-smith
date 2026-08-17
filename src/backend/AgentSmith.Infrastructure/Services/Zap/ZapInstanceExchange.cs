using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Zap;

/// <summary>
/// p0429a: reads the request and the response ZAP already reports per alert instance.
/// <para>
/// The report has carried <c>method</c>, <c>attack</c>, <c>evidence</c> and the raw
/// request/response headers and bodies all along; the parser kept the URI and dropped the
/// rest, which left every live-target finding with nothing a refuter could read. The
/// bodies are bounded here because a response body is unbounded and a refutation is about
/// the exchange, not about the payload's tail.
/// </para>
/// </summary>
internal static class ZapInstanceExchange
{
    private const int MaxBodyChars = 2000;

    internal static HttpExchange? From(JsonElement instance, string url)
    {
        if (instance.ValueKind != JsonValueKind.Object) return null;
        var method = Text(instance, "method");
        return new HttpExchange(
            string.IsNullOrWhiteSpace(method) ? "GET" : method,
            url,
            Text(instance, "attack"),
            Text(instance, "evidence"),
            Join(Text(instance, "request-header"), Text(instance, "request-body")),
            Join(Text(instance, "response-header"), Text(instance, "response-body")));
    }

    private static string? Join(string? header, string? body)
    {
        var head = header?.TrimEnd();
        var tail = Bounded(body);
        if (string.IsNullOrWhiteSpace(head)) return string.IsNullOrWhiteSpace(tail) ? null : tail;
        return string.IsNullOrWhiteSpace(tail) ? head : head + "\n\n" + tail;
    }

    private static string? Bounded(string? body) =>
        body is null || body.Length <= MaxBodyChars
            ? body
            : body[..MaxBodyChars] + $"\n… ({body.Length - MaxBodyChars} more characters)";

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
