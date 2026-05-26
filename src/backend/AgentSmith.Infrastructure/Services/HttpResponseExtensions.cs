using System.Net.Http;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Extension that reads the response body before throwing on HTTP errors.
/// Replaces EnsureSuccessStatusCode() which discards the error message.
/// </summary>
internal static class HttpResponseExtensions
{
    public static async Task EnsureSuccessWithBodyAsync(
        this HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = string.IsNullOrWhiteSpace(body)
            ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
            : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body)}";

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static string Truncate(string text) =>
        text.Length <= 500 ? text : text[..500] + "...";
}
