using System.Net.Http.Headers;
using System.Text.Json;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0517: drives a real hub invocation over SignalR's long-polling transport — negotiate,
/// handshake, invoke, read the completion — using nothing but an <see cref="HttpClient"/>.
/// <para>
/// The transport is the point, not a shortcut: the assertion that matters is that the
/// server's own dispatcher names the method the way the permission table does, and only a
/// real invocation through that dispatcher can say so. The test project carries no SignalR
/// client package, and long polling is plain HTTP.
/// </para>
/// </summary>
internal sealed class HubLongPoll(HttpClient client, string path, string? token)
{
    private const char RecordSeparator = '\u001e';
    private const string Handshake = """{"protocol":"json","version":1}""";

    /// <summary>Invokes one hub method and returns the completion message's raw JSON.</summary>
    public async Task<string> InvokeAsync(string method, params object?[] arguments)
    {
        var id = await NegotiateAsync();
        await SendAsync(id, Handshake, Invocation(method, arguments));
        return await PollForCompletionAsync(id);
    }

    private async Task<string> NegotiateAsync()
    {
        using var request = Post($"{path}/negotiate?negotiateVersion=1");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("connectionToken").GetString()!;
    }

    private async Task SendAsync(string id, params string[] messages)
    {
        using var request = Post($"{path}?id={Uri.EscapeDataString(id)}");
        request.Content = new StringContent(
            string.Concat(messages.Select(m => m + RecordSeparator)));
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
    }

    // The first poll starts the application, so the completion may land after it returns.
    // Polling again is what the real transport does; the deadline keeps a silent server
    // from parking the suite on the ninety-second server-side poll timeout.
    private async Task<string> PollForCompletionAsync(string id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"{path}?id={Uri.EscapeDataString(id)}");
            Authorize(request);
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var completion = (await response.Content.ReadAsStringAsync())
                .Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(m => m.Contains("\"type\":3", StringComparison.Ordinal));
            if (completion is not null) return completion;
        }
        throw new TimeoutException($"The hub never completed the invocation on {path}.");
    }

    private static string Invocation(string method, object?[] arguments) => JsonSerializer.Serialize(
        new { type = 1, invocationId = "1", target = method, arguments });

    private HttpRequestMessage Post(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        Authorize(request);
        return request;
    }

    private void Authorize(HttpRequestMessage request)
    {
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
