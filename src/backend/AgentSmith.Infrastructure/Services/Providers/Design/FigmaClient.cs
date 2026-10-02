using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Models.Design;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Design;

/// <summary>
/// 2026-10-01-7f7ab: Figma's REST API through a typed client whose base address is the API
/// host — the only host it ever calls, so a link can choose a file key, never where the token
/// goes. The token is resolved from the source's secret NAME on every request and set on that
/// request alone. A 429 with a short Retry-After is honoured once; anything longer is
/// reported with the wait, because whether to wait that long is the caller's decision.
/// <para>2026-10-01-7f7ac: an export asks the API for a render url and hands that url to the
/// token-less <see cref="IFigmaImageDownloader"/> — the token never leaves the API host.</para>
/// </summary>
public sealed class FigmaClient(
    HttpClient http, ISecretValues secrets, TimeProvider clock, IFigmaImageDownloader downloader,
    ILogger<FigmaClient> logger) : IFigmaClient
{
    internal static readonly Uri ApiHost = new("https://api.figma.com/");
    internal const string TokenHeader = "X-Figma-Token";
    private static readonly TimeSpan LongestHonouredWait = TimeSpan.FromSeconds(30);

    public Task<FigmaReadResult> GetNodesAsync(
        string secretName, string fileKey, string nodeId, int depth, string? version, CancellationToken cancellationToken) =>
        ReadAsync(secretName,
            $"v1/files/{Uri.EscapeDataString(fileKey)}/nodes?ids={Uri.EscapeDataString(nodeId)}&depth={depth}"
            + (string.IsNullOrWhiteSpace(version) ? string.Empty : $"&version={Uri.EscapeDataString(version.Trim())}"),
            cancellationToken);

    public Task<FigmaReadResult> GetLocalVariablesAsync(
        string secretName, string fileKey, CancellationToken cancellationToken) =>
        ReadAsync(secretName, $"v1/files/{Uri.EscapeDataString(fileKey)}/variables/local", cancellationToken);

    public async Task<FigmaExportResult> ExportPngAsync(
        string secretName, string fileKey, string nodeId, double scale, string? version, CancellationToken cancellationToken)
    {
        var read = await ReadAsync(secretName,
            $"v1/images/{Uri.EscapeDataString(fileKey)}?ids={Uri.EscapeDataString(nodeId)}&format=png"
            + $"&scale={scale.ToString("0.####", CultureInfo.InvariantCulture)}&use_absolute_bounds=true"
            + (string.IsNullOrWhiteSpace(version) ? string.Empty : $"&version={Uri.EscapeDataString(version.Trim())}"),
            cancellationToken);
        if (read.Body is not { } body)
            return FigmaExportResult.Failed(read.Failure!);
        return RenderUrl(body, nodeId) is { } url
            ? await downloader.DownloadPngAsync(url, cancellationToken)
            : FigmaExportResult.Failed("Figma rendered no image for this node — it may be invisible, empty or not renderable");
    }

    /// <summary>The images map's entry for the node; null (Figma's own answer for a failed render) or absent reads as none.</summary>
    private static string? RenderUrl(JsonElement body, string nodeId) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty("images", out var images)
        && images.ValueKind == JsonValueKind.Object && images.TryGetProperty(nodeId, out var url)
        && url.ValueKind == JsonValueKind.String ? url.GetString() : null;

    private async Task<FigmaReadResult> ReadAsync(string secretName, string path, CancellationToken ct)
    {
        var token = secrets.Resolve(secretName);
        if (string.IsNullOrEmpty(token))
            return FigmaReadResult.Failed(new FigmaReadFailure(
                FigmaReadFailureKind.Forbidden, $"secret '{secretName}' holds no token"));
        var first = await SendAsync(token, path, ct);
        if (first.Failure is not { Kind: FigmaReadFailureKind.RateLimited, RetryAfter: { } wait }
            || wait > LongestHonouredWait)
            return first;
        logger.LogInformation("Figma rate limit: retrying once after {Seconds}s", wait.TotalSeconds);
        await Task.Delay(wait, clock, ct);
        return await SendAsync(token, path, ct);
    }

    private async Task<FigmaReadResult> SendAsync(string token, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ApiHost, path));
        request.Headers.TryAddWithoutValidation(TokenHeader, token);
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return FigmaReadResult.Failed(FigmaResponseClassifier.Classify(response));
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return FigmaReadResult.Of(document.RootElement.Clone());
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // The exception's own message is not repeated: only its type leaves this method.
            logger.LogWarning("Figma read failed: {ExceptionType}", ex.GetType().Name);
            return FigmaReadResult.Failed(ex is JsonException
                ? new FigmaReadFailure(FigmaReadFailureKind.Unknown, "the response was not JSON")
                : new FigmaReadFailure(FigmaReadFailureKind.Unreachable, $"api.figma.com did not answer ({ex.GetType().Name})"));
        }
    }
}
