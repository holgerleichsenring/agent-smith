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
/// </summary>
public sealed class FigmaClient(
    HttpClient http, ISecretValues secrets, TimeProvider clock, ILogger<FigmaClient> logger) : IFigmaClient
{
    internal static readonly Uri ApiHost = new("https://api.figma.com/");
    internal const string TokenHeader = "X-Figma-Token";
    private static readonly TimeSpan LongestHonouredWait = TimeSpan.FromSeconds(30);

    public Task<FigmaReadResult> GetNodesAsync(
        string secretName, string fileKey, string nodeId, int depth, CancellationToken cancellationToken) =>
        ReadAsync(secretName,
            $"v1/files/{Uri.EscapeDataString(fileKey)}/nodes?ids={Uri.EscapeDataString(nodeId)}&depth={depth}",
            cancellationToken);

    public Task<FigmaReadResult> GetLocalVariablesAsync(
        string secretName, string fileKey, CancellationToken cancellationToken) =>
        ReadAsync(secretName, $"v1/files/{Uri.EscapeDataString(fileKey)}/variables/local", cancellationToken);

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
