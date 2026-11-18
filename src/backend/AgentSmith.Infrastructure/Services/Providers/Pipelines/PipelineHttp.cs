using System.Net;
using System.Text;
using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Pipelines;

/// <summary>
/// 2026-11-18-3863e: the authorised JSON and text calls a pipeline runner makes to its host's REST API. A host's refusal
/// throws with its own words (<see cref="HttpResponseExtensions.EnsureSuccessWithBodyAsync"/>); a 204 answers null.
/// </summary>
public sealed class PipelineHttp(HttpClient http, Action<HttpRequestMessage> authorize)
{
    public async Task<JsonDocument?> SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = Request(method, url);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        await response.EnsureSuccessWithBodyAsync(ct);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    public async Task<JsonElement> GetAsync(string url, CancellationToken ct)
    {
        using var document = await SendAsync(HttpMethod.Get, url, null, ct)
            ?? throw new HttpRequestException($"{url} answered no content");
        return document.RootElement.Clone();
    }

    /// <summary>A log's text, or a line saying why it could not be read — a log costs the tail, never the evidence.</summary>
    public async Task<string> TextAsync(string url, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, url);
        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsStringAsync(ct)
            : $"(log not readable: the host answered {(int)response.StatusCode})";
    }

    public static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private HttpRequestMessage Request(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        authorize(request);
        return request;
    }
}
