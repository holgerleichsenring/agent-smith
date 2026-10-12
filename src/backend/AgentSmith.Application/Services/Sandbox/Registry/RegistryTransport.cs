using System.Net.Http.Headers;
using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services.Browser;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: one exchange with an image registry or its token service. The host is
/// model-authored, so it is judged by the same public-address guard a render is before any
/// request leaves; the named client follows no redirect and buffers a bounded body. Every
/// failure to get an answer comes back as <see cref="RegistryReply.Failure"/>, never thrown —
/// a lookup that cannot answer must not stop a write. The caller's own cancellation still
/// propagates.
/// </summary>
public sealed class RegistryTransport(
    IHttpClientFactory clients, RenderUrlGuard guard, ILogger<RegistryTransport> logger)
{
    public const string ClientName = "image-registry";
    public const int StepTimeoutSeconds = 5;
    public const long MaxResponseBytes = 4L * 1024 * 1024;

    public async Task<RegistryReply> SendAsync(
        HttpMethod method, Uri url, string accept, string? bearer, CancellationToken cancellationToken)
    {
        if (await guard.RefusalForAsync(url, cancellationToken) is { } refusal)
            return RegistryReply.Unanswered($"not looked up: {refusal}");
        using var request = Request(method, url, accept, bearer);
        return await ExchangeAsync(request, bearer, cancellationToken);
    }

    private async Task<RegistryReply> ExchangeAsync(
        HttpRequestMessage request, string? bearer, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!;
        using var step = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        step.CancelAfter(TimeSpan.FromSeconds(StepTimeoutSeconds));
        try
        {
            using var response = await clients.CreateClient(ClientName).SendAsync(request, step.Token);
            return await ReplyAsync(response, bearer, step.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Image registry {Host} gave no answer in time", url.Host);
            return RegistryReply.Unanswered($"{url.Host} gave no answer within {StepTimeoutSeconds} s");
        }
        catch (HttpRequestException ex)
        {
            logger.LogDebug(ex, "Image registry {Host} could not be reached", url.Host);
            return RegistryReply.Unanswered($"{url.Host} could not be reached ({ex.HttpRequestError})");
        }
    }

    private static HttpRequestMessage Request(HttpMethod method, Uri url, string accept, string? bearer)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Accept", accept);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }

    private static async Task<RegistryReply> ReplyAsync(
        HttpResponseMessage response, string? bearer, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new RegistryReply(
            (int)response.StatusCode, body, Header(response, "WWW-Authenticate"),
            Header(response, "Link"), bearer, null);
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
}
