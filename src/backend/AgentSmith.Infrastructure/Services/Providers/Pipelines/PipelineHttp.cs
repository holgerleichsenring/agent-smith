using System.Net;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Providers;

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

    /// <summary>2026-11-23-933dd: a download's bytes in a seekable stream, or null when the host answers 404 or 410 (an
    /// absent or expired artifact). Any other refusal throws in the host's words.</summary>
    public async Task<Stream?> DownloadAsync(string url, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, url);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return null;
        await response.EnsureSuccessWithBodyAsync(ct);
        var copy = new MemoryStream();
        await response.Content.CopyToAsync(copy, ct);
        copy.Position = 0;
        return copy;
    }

    /// <summary>2026-11-23-933dd: one raw file as text, read only up to <paramref name="maxBytes"/>: a larger one answers
    /// too-large with its size (the declared length, or what was read when the host declares none); 404 answers null.</summary>
    public async Task<PipelineArtifactText?> BoundedTextAsync(string url, long maxBytes, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, url);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return null;
        await response.EnsureSuccessWithBodyAsync(ct);
        if (response.Content.Headers.ContentLength is { } declared && declared > maxBytes)
            return PipelineArtifactText.TooLarge(declared);
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        var buffer = await HeadAsync(body, maxBytes + 1, ct);
        return buffer.Length > maxBytes
            ? PipelineArtifactText.TooLarge(buffer.Length)
            : PipelineArtifactText.Of(Encoding.UTF8.GetString(buffer.ToArray()), buffer.Length);
    }

    private const int ChunkBytes = 81920; // not a limit: the read buffer's size, the bound is maxBytes

    // At most `limit` bytes of the body: one more than the bound is enough to know it is over.
    private static async Task<MemoryStream> HeadAsync(Stream body, long limit, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[ChunkBytes];
        int read;
        while (buffer.Length < limit && (read = await body.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, limit - buffer.Length)), ct)) > 0)
            buffer.Write(chunk, 0, read);
        return buffer;
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
